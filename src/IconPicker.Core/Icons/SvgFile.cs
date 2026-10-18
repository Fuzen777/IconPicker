using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace IconPicker.Core.Icons;

/// <summary>The size an SVG's content is drawn in, in its own units: its viewBox, or else its width and height.</summary>
public readonly record struct SvgBox(float Width, float Height)
{
    public bool IsNearlySquare => Math.Max(Width, Height) - Math.Min(Width, Height) <= Math.Max(Width, Height) * 0.05f;
}

/// <summary>
/// An SVG parsed and cleaned of everything that reaches outside the file (other files, the network, stylesheets, fonts, scripts), ready to render or recolor.
/// </summary>
public sealed partial class SvgFile
{
    /// <summary>The folder search skips larger SVGs: icons are small, and big drawings are slow to render.</summary>
    public const long MaximumSearchedFileSize = 1024 * 1024;

    /// <summary>The largest SVG Browse and the builder open.</summary>
    public const long MaximumFileSize = 16 * 1024 * 1024;

    private const long MaximumCharactersFromEntities = 10_000_000;

    /// <summary>Svg.Skia draws nested elements recursively: a much deeper document would overflow the stack, which can't be caught.</summary>
    private const int MaximumDepth = 256;

    /// <summary>Same-file references (&lt;use&gt;, patterns, gradients) multiply what is drawn; a few chained ones would take minutes and gigabytes.</summary>
    private const long MaximumDrawnElements = 100_000;

    private static readonly string[] PaintAttributeNames = ["fill", "stroke", "stop-color", "flood-color", "color"];

    private readonly XDocument _document;

    private SvgFile(XDocument document)
    {
        _document = document;
        Box = ReadBox(document.Root!.Attribute("viewBox")?.Value, document.Root.Attribute("width")?.Value, document.Root.Attribute("height")?.Value);
        HasMask = document.Descendants().Any(element => element.Name.LocalName == "mask");
    }

    /// <summary>Whether the SVG uses masks, whose own black and white paints decide what shows: recoloring would fill in their cut-outs.</summary>
    public bool HasMask { get; }

    /// <summary>The size the content is drawn in, or null when the SVG doesn't say.</summary>
    public SvgBox? Box { get; }

    /// <summary>The SVG in the bytes, cleaned, or null when they aren't a readable SVG.</summary>
    public static SvgFile? TryParse(byte[] svgBytes)
    {
        try
        {
            using var memory = new MemoryStream(svgBytes);
            using var reader = XmlReader.Create(memory, CreateReaderSettings());
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Root is not { Name.LocalName: "svg" })
            {
                return null;
            }
            RemoveExternalReferences(document);
            return IsWithinDrawingLimits(document) ? new SvgFile(document) : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>The SVG file, cleaned, or null when it can't be read, is larger than <see cref="MaximumFileSize"/> or isn't a readable SVG.</summary>
    public static SvgFile? TryLoad(string filePath)
    {
        try
        {
            var file = new FileInfo(filePath);
            return file.Length > MaximumFileSize ? null : TryParse(File.ReadAllBytes(filePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>The size an SVG file's content is drawn in, reading only its root element. Null when it can't be read, isn't an SVG or doesn't say.</summary>
    public static SvgBox? ReadBox(string filePath)
    {
        try
        {
            using var reader = XmlReader.Create(filePath, CreateReaderSettings());
            if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "svg")
            {
                return null;
            }
            return ReadBox(reader.GetAttribute("viewBox"), reader.GetAttribute("width"), reader.GetAttribute("height"));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>The cleaned SVG as UTF-8 bytes.</summary>
    public byte[] ToBytes()
    {
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
        {
            _document.Save(writer);
        }
        return output.ToArray();
    }

    /// <summary>
    /// A copy with every solid paint (attributes, inline styles, &lt;style&gt; rules, gradient stops, currentColor) replaced by one color. The root gets the color and a fill too, so SVGs with no color at all, and templates that draw nothing until colored (<c>context-fill</c>), are recolored as well.
    /// </summary>
    /// <param name="hexColor">A CSS color such as <c>#1E90FF</c>.</param>
    public SvgFile Recolor(string hexColor)
    {
        var document = new XDocument(_document);
        var root = document.Root!;
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attributeName in PaintAttributeNames)
            {
                var attribute = element.Attribute(attributeName);
                if (attribute is not null && IsReplaceablePaint(attribute.Value))
                {
                    attribute.Value = hexColor;
                }
            }
            if (element.Attribute("style") is { } style)
            {
                style.Value = ReplacePaintDeclarations(style.Value, hexColor);
            }
            if (element.Name.LocalName == "style")
            {
                foreach (var textNode in element.Nodes().OfType<XText>())
                {
                    textNode.Value = ReplacePaintDeclarations(textNode.Value, hexColor);
                }
            }
        }
        root.SetAttributeValue("color", hexColor);
        var rootStyle = root.Attribute("style")?.Value ?? "";
        if (root.Attribute("fill") is null && !rootStyle.Contains("fill", StringComparison.OrdinalIgnoreCase))
        {
            root.SetAttributeValue("fill", hexColor);
        }
        return new SvgFile(document);
    }

    private static XmlReaderSettings CreateReaderSettings()
    {
        // The DTD is read for the entities some editors define (Illustrator's &ns_svg;), but nothing outside the file is ever resolved.
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = MaximumCharactersFromEntities,
        };
    }

    /// <summary>
    /// Removes every way an SVG can load something else: links to other files or the web (only same-file <c>#id</c> and embedded <c>data:</c> links stay), <c>url()</c> references to other files, stylesheet instructions, imported stylesheets, web fonts, scripts and event handlers. The DOCTYPE goes too, its entities already expanded.
    /// </summary>
    private static void RemoveExternalReferences(XDocument document)
    {
        document.DocumentType?.Remove();
        foreach (var instruction in document.DescendantNodes().OfType<XProcessingInstruction>().ToList())
        {
            instruction.Remove();
        }
        foreach (var script in document.Descendants().Where(element => element.Name.LocalName == "script").ToList())
        {
            script.Remove();
        }
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes().ToList())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }
                var name = attribute.Name.LocalName;
                if ((name == "href" && !IsSameFileLink(attribute.Value)) || name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Remove();
                }
                else if (attribute.Value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Value = RemoveExternalUrls(attribute.Value);
                }
            }
            if (element.Name.LocalName == "style")
            {
                foreach (var textNode in element.Nodes().OfType<XText>())
                {
                    textNode.Value = RemoveExternalUrls(FontFacePattern().Replace(ImportPattern().Replace(textNode.Value, ""), ""));
                }
            }
        }
    }

    /// <summary>
    /// Whether Svg.Skia can draw the document safely: no deeper than <see cref="MaximumDepth"/> and drawing at most <see cref="MaximumDrawnElements"/> elements, counting each same-file reference as a copy of what it points to. A reference loop counts as too much.
    /// </summary>
    private static bool IsWithinDrawingLimits(XDocument document)
    {
        // The document's own depth first, without recursion, so measuring it can't overflow the stack either.
        var pending = new Stack<(XElement Element, int Depth)>();
        pending.Push((document.Root!, 1));
        while (pending.Count > 0)
        {
            var (element, depth) = pending.Pop();
            if (depth > MaximumDepth)
            {
                return false;
            }
            foreach (var child in element.Elements())
            {
                pending.Push((child, depth + 1));
            }
        }
        var elementsById = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var element in document.Descendants())
        {
            if (element.Attribute("id")?.Value is { } id)
            {
                elementsById.TryAdd(id, element);
            }
        }
        var measured = new Dictionary<XElement, (int Depth, long Count)>();
        var measuring = new HashSet<XElement>();

        // Recursion stays within MaximumDepth levels, references included.
        (int Depth, long Count)? Measure(XElement element, int level)
        {
            if (level > MaximumDepth)
            {
                return null;
            }
            if (measured.TryGetValue(element, out var known))
            {
                return known;
            }
            if (!measuring.Add(element))
            {
                return null;
            }
            var depth = 1;
            long count = 1;
            var referenced = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "href")?.Value.Trim() is ['#', .. var targetId]
                && elementsById.TryGetValue(targetId, out var target) ? target : null;
            foreach (var part in referenced is null ? element.Elements() : element.Elements().Prepend(referenced))
            {
                if (Measure(part, level + 1) is not { } size)
                {
                    return null;
                }
                depth = Math.Max(depth, size.Depth + 1);
                count += size.Count;
                if (count > MaximumDrawnElements || depth > MaximumDepth)
                {
                    return null;
                }
            }
            measuring.Remove(element);
            measured[element] = (depth, count);
            return (depth, count);
        }

        return Measure(document.Root!, 1) is not null;
    }

    private static bool IsSameFileLink(string link)
    {
        var trimmed = link.Trim();
        return trimmed.StartsWith('#') || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveExternalUrls(string value)
    {
        return UrlPattern().Replace(value, match => match.Groups["target"].Value.Trim().StartsWith('#') ? match.Value : "none");
    }

    private static SvgBox? ReadBox(string? viewBox, string? width, string? height)
    {
        if (viewBox is not null)
        {
            var numbers = viewBox.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length == 4
                && float.TryParse(numbers[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxWidth)
                && float.TryParse(numbers[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxHeight)
                && boxWidth > 0 && boxHeight > 0 && float.IsFinite(boxWidth) && float.IsFinite(boxHeight))
            {
                return new SvgBox(boxWidth, boxHeight);
            }
        }
        var widthInPixels = LengthToPixels(width);
        var heightInPixels = LengthToPixels(height);
        return (widthInPixels, heightInPixels) switch
        {
            ( > 0, > 0) => new SvgBox(widthInPixels.Value, heightInPixels.Value),
            ( > 0, _) => new SvgBox(widthInPixels.Value, widthInPixels.Value),
            (_, > 0) => new SvgBox(heightInPixels.Value, heightInPixels.Value),
            _ => null,
        };
    }

    private static float? LengthToPixels(string? length)
    {
        if (string.IsNullOrWhiteSpace(length))
        {
            return null;
        }
        var match = LengthPattern().Match(length.Trim());
        if (!match.Success || !float.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
        {
            return null;
        }
        return match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "" or "px" => value,
            "pt" => value * 96f / 72f,
            "pc" => value * 16f,
            "mm" => value * 96f / 25.4f,
            "cm" => value * 96f / 2.54f,
            "in" => value * 96f,
            "em" => value * 16f,
            "ex" => value * 8f,
            // Percentages and anything else give no size of their own.
            _ => null,
        };
    }

    private static bool IsReplaceablePaint(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed.Length > 0 && trimmed is not ("none" or "transparent" or "inherit") && !trimmed.StartsWith("url(", StringComparison.Ordinal);
    }

    private static string ReplacePaintDeclarations(string css, string hexColor)
    {
        return PaintDeclarationPattern().Replace(css, match => IsReplaceablePaint(match.Groups["value"].Value) ? $"{match.Groups["name"].Value}:{hexColor}" : match.Value);
    }

    [GeneratedRegex(@"^(?<value>[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)\s*(?<unit>[a-zA-Z%]*)$")]
    private static partial Regex LengthPattern();

    // fill, stroke, stop-color, flood-color and color declarations inside style="" or <style>.
    [GeneratedRegex(@"(?<name>(?<![\w-])(?:fill|stroke|stop-color|flood-color|color))\s*:\s*(?<value>[^;}""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PaintDeclarationPattern();

    [GeneratedRegex(@"url\(\s*(?<quote>['""]?)(?<target>[^'"")]*)\k<quote>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"@import[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"@font-face\s*\{[^}]*\}", RegexOptions.IgnoreCase)]
    private static partial Regex FontFacePattern();
}

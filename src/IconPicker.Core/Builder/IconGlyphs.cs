using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Library;

namespace IconPicker.Core.Builder;

/// <summary>What a built icon draws on its background.</summary>
public interface IIconGlyph
{
    /// <summary>Whether it can take one color: only an SVG can, and only when it is drawn in one color.</summary>
    bool CanRecolor { get; }

    /// <summary>The glyph filling a size × size square, centered and keeping its proportions, as premultiplied BGRA. Null when it can't be drawn.</summary>
    Task<byte[]?> RenderAsync(int size);
}

/// <summary>An SVG, drawn afresh at every size.</summary>
public sealed class VectorGlyph(SvgFile svg) : IIconGlyph
{
    private readonly Lazy<SvgColors> _colors = new(() => SvgRenderer.AnalyzeColors(svg));

    public SvgFile Svg { get; } = svg;

    /// <summary>Single when everything visible has one color; also when nothing is visible yet, as with templates waiting for a color.</summary>
    public SvgColors Colors => _colors.Value;

    public bool CanRecolor => Colors == SvgColors.Single;

    public VectorGlyph WithColor(string hexColor)
    {
        return new VectorGlyph(Svg.Recolor(hexColor));
    }

    public Task<byte[]?> RenderAsync(int size)
    {
        return Task.FromResult(SvgRenderer.Render(Svg, size).Pixels);
    }
}

/// <summary>A picture, an .ico file or an executable's icon, drawn from its own image closest to each size.</summary>
public sealed class RasterGlyph(IconSource source) : IIconGlyph
{
    public IconSource Source { get; } = source;

    public bool CanRecolor => false;

    public async Task<byte[]?> RenderAsync(int size)
    {
        using var bitmap = await IconPreviewRenderer.RenderAsync(Source, size, IconFit.Fill).ConfigureAwait(false);
        // A picture that isn't square is centered with transparent padding.
        return bitmap is null ? null : IconPreviewRenderer.ToSquarePixels(bitmap, size);
    }
}

/// <summary>A glyph with what a saved icon keeps of it: the file it was drawn from, and its name.</summary>
/// <param name="Glyph">What is drawn.</param>
/// <param name="FileBytes">The source as a saved icon stores it: an SVG cleaned of outside references, an executable's icon as an .ico file, or the file itself.</param>
/// <param name="FileExtension">The stored source's extension, with its dot.</param>
/// <param name="Name">A name for the built icon, from the source.</param>
public sealed record GlyphSource(IIconGlyph Glyph, byte[] FileBytes, string FileExtension, string Name, string? BuiltInIconId = null, BuiltInIconStyle? BuiltInIconStyle = null)
{
    /// <summary>A built-in icon's glyph, or null when its SVG can't be read.</summary>
    public static GlyphSource? FromBuiltIn(BuiltInIcon icon, BuiltInIconStyle style, byte[] svgBytes)
    {
        return SvgFile.TryParse(svgBytes) is { } svg ? new GlyphSource(new VectorGlyph(svg), svg.ToBytes(), ".svg", icon.Name, icon.Id, style) : null;
    }

    /// <summary>Any icon the app can use as a glyph, or null when it can't be read.</summary>
    public static GlyphSource? FromFile(IconSource source)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(source.FilePath);
            switch (source.Kind)
            {
                case IconSourceKind.Vector:
                    return SvgFile.TryLoad(source.FilePath) is { } svg ? new GlyphSource(new VectorGlyph(svg), svg.ToBytes(), ".svg", name) : null;
                case IconSourceKind.Module:
                    // Only the chosen icon is kept, not the whole executable.
                    return ExeIconExtractor.ExtractIconFile(source.FilePath, source.IconIndex) is { } iconFile
                        ? new GlyphSource(new RasterGlyph(source), iconFile, ".ico", name)
                        : null;
                default:
                    return new GlyphSource(new RasterGlyph(source), File.ReadAllBytes(source.FilePath), Path.GetExtension(source.FilePath).ToLowerInvariant(), name);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}

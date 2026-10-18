using System.Text.Json.Serialization;

namespace IconPicker.Core.Candidates;

public enum IconSourceKind
{
    /// <summary>An icon inside an .exe or .dll file.</summary>
    Module,

    /// <summary>An .ico file.</summary>
    IconFile,

    /// <summary>A picture (PNG, JPG…), converted to an .ico when used.</summary>
    Image,

    /// <summary>An SVG, drawn at every size of an .ico when used.</summary>
    Vector,
}

/// <summary>A file, and the icon inside it, that can become a folder's icon.</summary>
/// <param name="FilePath">The file's full path.</param>
/// <param name="Kind">What kind of file it is.</param>
/// <param name="IconIndex">For an .exe or .dll, which of its icons; 0 otherwise.</param>
/// <param name="IsBuilt">An icon made in the builder and kept among the app's data. It is always copied, even into a folder that holds the app's data, so editing or deleting it never changes a folder.</param>
public sealed record IconSource(string FilePath, IconSourceKind Kind, int IconIndex = 0, bool IsBuilt = false)
{
    private static readonly HashSet<string> ModuleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

    /// <summary>Every extension the app can take an icon from, for file pickers.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".ico", .. ModuleExtensions, .. ImageExtensions, ".svg"];

    /// <summary>A picture or an SVG, which has to be converted to an .ico, so it is always copied and never linked.</summary>
    [JsonIgnore]
    public bool IsConverted => Kind is IconSourceKind.Image or IconSourceKind.Vector;

    /// <summary>The kind of icon source a file is, from its extension, or null when it can't be one.</summary>
    public static IconSourceKind? GetKind(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase))
        {
            return IconSourceKind.IconFile;
        }
        if (ModuleExtensions.Contains(extension))
        {
            return IconSourceKind.Module;
        }
        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            return IconSourceKind.Vector;
        }
        return ImageExtensions.Contains(extension) ? IconSourceKind.Image : null;
    }
}

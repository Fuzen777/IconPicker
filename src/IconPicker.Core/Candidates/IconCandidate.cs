using IconPicker.Core.Builder;

namespace IconPicker.Core.Candidates;

/// <summary>An icon found inside a folder, ready to be ranked and shown.</summary>
/// <param name="Source">Where the icon comes from.</param>
/// <param name="RelativePath">The file's path relative to the searched folder, or its full path when it lies elsewhere.</param>
/// <param name="Depth">How many folder levels below the searched folder the file sits (0 = directly in it).</param>
/// <param name="FileSize">The file's size in bytes.</param>
/// <param name="Width">The icon's or image's width in pixels (the largest size for icons).</param>
/// <param name="Height">The icon's or image's height in pixels.</param>
/// <param name="ProductName">For an .exe or .dll, the product name in its version information, which can name the app when the file name doesn't (SandMan.exe is "Sandboxie-Plus").</param>
/// <param name="ShortcutPath">For an icon shown by a shortcut (.lnk, .url) inside the folder, the shortcut's full path. <paramref name="RelativePath"/> and <paramref name="Depth"/> then describe the shortcut, while <paramref name="Source"/> is the file its icon comes from.</param>
/// <param name="Built">For an icon made in the builder, what it was made from. <paramref name="Source"/> is then the finished .ico file, kept in My icons or among the app's built icons.</param>
public sealed record IconCandidate(IconSource Source, string RelativePath, int Depth, long FileSize, int Width, int Height, string? ProductName = null, string? ShortcutPath = null, BuiltIconOrigin? Built = null)
{
    private const double SquareTolerance = 0.05;

    public int SmallestSide => Math.Min(Width, Height);

    public int LargestSide => Math.Max(Width, Height);

    /// <summary>Square, or close enough (sides within 5%) that the transparent padding is barely visible: off-by-one exports, trimmed sprites.</summary>
    public bool IsNearlySquare => LargestSide - SmallestSide <= LargestSide * SquareTolerance;

    public bool IsShortcut => ShortcutPath is not null;
}

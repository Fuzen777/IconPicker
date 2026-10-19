using IconPicker.App.Services;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;

namespace IconPicker.App.ViewModels;

/// <summary>Built icons as candidates of a folder's picker: their finished .ico among the app's data, always copied into the folder.</summary>
internal static class BuiltIconCandidates
{
    public static IconCandidate Create(string iconPath, BuiltIconOrigin origin)
    {
        var largestSize = ImageIconConverter.IconSizes[0];
        return new IconCandidate(new IconSource(iconPath, IconSourceKind.IconFile, IsAppData: true), iconPath, Depth: 0, new FileInfo(iconPath).Length, largestSize, largestSize, Built: origin);
    }

    /// <summary>
    /// A saved icon as it is now, copied among the built icons: editing it later (Save the changes to My icons) rewrites its icon.ico, which must not change picks already made for other folders.
    /// </summary>
    public static async Task<IconCandidate> FromSavedIconAsync(SavedIcon savedIcon, BuiltIconCache builtIconCache, GlyphSource? source = null)
    {
        var iconPath = await builtIconCache.WriteAsync(await File.ReadAllBytesAsync(savedIcon.IconPath));
        return Create(iconPath, new BuiltIconOrigin(source ?? savedIcon.LoadGlyphSource(), savedIcon.Recipe.Style, savedIcon.Recipe.Name, savedIcon.Id));
    }
}

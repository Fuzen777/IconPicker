using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Recent;

namespace IconPicker.App.ViewModels;

/// <summary>"Single"'s recent icons as candidates: their copy among the app's data, always copied into the folder.</summary>
internal static class RecentIconCandidates
{
    /// <summary>Null when the copy is gone.</summary>
    public static IconCandidate? Create(RecentIconStore store, RecentIcon recentIcon)
    {
        var iconPath = store.GetIconPath(recentIcon);
        try
        {
            var largestSize = IcoFileBuilder.ReadImageSizes(iconPath) is { Count: > 0 } sizes ? sizes.Max(size => Math.Max(size.Width, size.Height)) : ImageIconConverter.IconSizes[0];
            return new IconCandidate(new IconSource(iconPath, IconSourceKind.IconFile, IsAppData: true), iconPath, Depth: 0, new FileInfo(iconPath).Length, largestSize, largestSize, Recent: recentIcon);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

using IconPicker.Core.Rules;

namespace IconPicker.Core.Detection;

public enum FolderMode
{
    /// <summary>Pick an icon for the folder itself.</summary>
    Single,

    /// <summary>Pick an icon for each of its subfolders.</summary>
    Batch,
}

/// <summary>The mode a folder opens in.</summary>
/// <param name="Mode">Single or batch.</param>
/// <param name="IsDeep">Whether batch looks for app folders at any depth instead of taking the direct subfolders.</param>
public sealed record ModeDetection(FolderMode Mode, bool IsDeep);

public static class ModeDetector
{
    public static ModeDetection Detect(DirectoryInfo folder)
    {
        // A drive itself can't take a folder icon, and its games sit deeper than its direct folders.
        if (FolderSearchRules.IsDriveRoot(folder))
        {
            return new ModeDetection(FolderMode.Batch, IsDeep: true);
        }
        if (AppFolderDetector.IsAppFolder(folder))
        {
            return new ModeDetection(FolderMode.Single, IsDeep: false);
        }
        return new ModeDetection(FolderMode.Batch, IsDeep: false);
    }
}

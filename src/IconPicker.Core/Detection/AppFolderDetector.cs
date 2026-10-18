using System.Security;
using IconPicker.Core.Icons;
using IconPicker.Core.Rules;

namespace IconPicker.Core.Detection;

/// <summary>An .exe that can give a folder its icon.</summary>
/// <param name="File">The executable.</param>
/// <param name="Depth">How many bin-style folders lie between it and the folder.</param>
public sealed record ExeCandidate(FileInfo File, int Depth);

/// <summary>A game or app folder found by the deep search.</summary>
/// <param name="Folder">The folder holding the executable, directly or in bin-style subfolders.</param>
/// <param name="OuterFolder">A folder above it whose only app this is (Game\Release\game.exe gives Game its icon too), or null.</param>
public sealed record AppFolder(DirectoryInfo Folder, DirectoryInfo? OuterFolder);

/// <summary>Finds game and app folders, ported from Get-CandidateExes and Find-AppFolders in Set Folder Icons.ps1.</summary>
public static class AppFolderDetector
{
    /// <summary>The .exe files directly in the folder and in its bin-style subfolders.</summary>
    public static List<ExeCandidate> GetCandidateExes(DirectoryInfo folder)
    {
        var candidates = new List<ExeCandidate>();
        AddCandidateExes(folder, 0, candidates);
        return candidates;
    }

    /// <summary>A game or app folder: it holds an .exe with an icon, directly or in a bin-style subfolder.</summary>
    public static bool IsAppFolder(DirectoryInfo folder)
    {
        return GetCandidateExes(folder).Exists(candidate => ExeIconExtractor.CountIcons(candidate.File.FullName) > 0);
    }

    /// <summary>
    /// Walks down from a folder and stops at each game or app folder: one with a candidate .exe. Other folders are only searched deeper.
    /// </summary>
    public static List<AppFolder> FindAppFolders(DirectoryInfo startFolder, CancellationToken cancellationToken)
    {
        var appFolders = new List<AppFolder>();
        FindAppFolders(startFolder, appFolders, canBeOuterFolder: true, cancellationToken);
        return appFolders;
    }

    private static void FindAppFolders(DirectoryInfo folder, List<AppFolder> appFolders, bool canBeOuterFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (GetCandidateExes(folder).Count > 0)
        {
            appFolders.Add(new AppFolder(folder, null));
            // Steam is an app holding games, so its library is still searched. The library folder itself never takes a game's icon, even when it holds a single game.
            var steamLibraryPath = Path.Combine(folder.FullName, "steamapps", "common");
            if (Directory.Exists(steamLibraryPath))
            {
                FindAppFolders(new DirectoryInfo(steamLibraryPath), appFolders, canBeOuterFolder: false, cancellationToken);
            }
            return;
        }
        var firstFoundIndex = appFolders.Count;
        foreach (var subFolder in FolderSearchRules.GetFoldersToSearch(folder))
        {
            // A repository counts when its app sits at its top (a downloaded tool); deeper .exe files are build outputs, so it isn't searched further.
            if (FolderSearchRules.IsRepository(subFolder) && GetCandidateExes(subFolder).Count == 0)
            {
                continue;
            }
            FindAppFolders(subFolder, appFolders, canBeOuterFolder: true, cancellationToken);
        }
        // A folder whose only app sits in an odd subfolder (Game\Release\game.exe) gets that app's icon too. This runs on the way back up, so the outermost such folder wins. Drives can't take a folder icon.
        if (canBeOuterFolder && appFolders.Count - firstFoundIndex == 1 && !FolderSearchRules.IsDriveRoot(folder))
        {
            appFolders[firstFoundIndex] = appFolders[firstFoundIndex] with { OuterFolder = folder };
        }
    }

    private static void AddCandidateExes(DirectoryInfo folder, int depth, List<ExeCandidate> candidates)
    {
        FileInfo[] exeFiles;
        try
        {
            exeFiles = folder.GetFiles("*.exe");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            exeFiles = [];
        }
        foreach (var exeFile in exeFiles)
        {
            // "*.exe" also matches longer extensions such as .exe1. A cloud-only file would be downloaded just to read its icon.
            if (string.Equals(exeFile.Extension, ".exe", StringComparison.OrdinalIgnoreCase)
                && !FolderSearchRules.IsIgnoredExe(exeFile.Name)
                && !FolderSearchRules.IsCloudPlaceholder(exeFile.Attributes))
            {
                candidates.Add(new ExeCandidate(exeFile, depth));
            }
        }
        foreach (var subFolder in FolderSearchRules.GetSubFolders(folder))
        {
            if (!subFolder.Attributes.HasFlag(FileAttributes.ReparsePoint) && FolderSearchRules.IsBinStyleFolder(subFolder, folder))
            {
                AddCandidateExes(subFolder, depth + 1, candidates);
            }
        }
    }
}

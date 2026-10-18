using IconPicker.Core.Rules;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Detection;

/// <summary>A folder that gets a row in batch mode.</summary>
/// <param name="Folder">The folder to give an icon.</param>
/// <param name="SearchNames">The names an icon file named like the folder may carry: its own, plus its app folder's or outer folder's in the deep search.</param>
/// <param name="Shortcut">For a shortcut to a folder, the shortcut, which shows the icon too. <paramref name="Folder"/> is then the folder it opens.</param>
public sealed record BatchRowTarget(DirectoryInfo Folder, IReadOnlyList<string> SearchNames, FileInfo? Shortcut = null)
{
    /// <summary>The row's name: the shortcut's, without its extension, or the folder's.</summary>
    public string Name => Shortcut is null ? Folder.Name : Path.GetFileNameWithoutExtension(Shortcut.Name);
}

/// <summary>Lists the folders batch mode works on.</summary>
public static class BatchRowSource
{
    /// <summary>
    /// One row per direct subfolder worth searching (Games → Game 1, Game 2…), and per shortcut to a folder (Games → Steam.lnk, searched in the folder it opens), sorted by name. Hidden folders and shortcuts get none.
    /// </summary>
    public static List<BatchRowTarget> GetDirectRows(DirectoryInfo folder)
    {
        return FolderSearchRules.GetFoldersToSearch(folder)
            .Where(subFolder => !FolderSearchRules.IsHiddenFolder(subFolder))
            .Select(subFolder => new BatchRowTarget(subFolder, [subFolder.Name]))
            .Concat(GetShortcutRows(folder))
            .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IEnumerable<BatchRowTarget> GetShortcutRows(DirectoryInfo folder)
    {
        FileInfo[] shortcuts;
        try
        {
            shortcuts = folder.GetFiles("*.lnk");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            yield break;
        }
        foreach (var shortcut in shortcuts)
        {
            if (shortcut.Attributes.HasFlag(FileAttributes.Hidden) || ShortcutReader.ReadFolderTarget(shortcut.FullName) is not { } target)
            {
                continue;
            }
            var shortcutName = Path.GetFileNameWithoutExtension(shortcut.Name);
            IReadOnlyList<string> searchNames = string.Equals(shortcutName, target.Name, StringComparison.OrdinalIgnoreCase) ? [shortcutName] : [shortcutName, target.Name];
            yield return new BatchRowTarget(target, searchNames, shortcut);
        }
    }

    /// <summary>
    /// One row per app folder at any depth, plus a row for each outer folder whose only app it is, as in the script. The search walks through hidden folders, but they get no row themselves.
    /// </summary>
    public static List<BatchRowTarget> GetDeepRows(DirectoryInfo folder, CancellationToken cancellationToken)
    {
        var rows = new List<BatchRowTarget>();
        foreach (var appFolder in AppFolderDetector.FindAppFolders(folder, cancellationToken))
        {
            if (appFolder.OuterFolder is null)
            {
                AddUnlessHidden(rows, appFolder.Folder, [appFolder.Folder.Name]);
                continue;
            }
            AddUnlessHidden(rows, appFolder.Folder, [appFolder.Folder.Name, appFolder.OuterFolder.Name]);
            AddUnlessHidden(rows, appFolder.OuterFolder, [appFolder.OuterFolder.Name, appFolder.Folder.Name]);
        }
        // Outer folders were added after their app folder; sorting puts each one right above it.
        rows.Sort((first, second) => StringComparer.CurrentCultureIgnoreCase.Compare(first.Folder.FullName, second.Folder.FullName));
        return rows;
    }

    private static void AddUnlessHidden(List<BatchRowTarget> rows, DirectoryInfo folder, IReadOnlyList<string> searchNames)
    {
        if (!FolderSearchRules.IsHiddenFolder(folder))
        {
            rows.Add(new BatchRowTarget(folder, searchNames));
        }
    }
}

using IconPicker.Core.Shell;

namespace IconPicker.Core.Candidates;

/// <summary>Paths handed to the app by a drop or a paste: a folder to open, and the files it can take icons from.</summary>
/// <param name="FolderPath">The first folder, or shortcut to one; it opens when nothing takes the icon files.</param>
/// <param name="IconFiles">Files Browse could pick, and shortcuts that aren't to a folder (their icon is used), in order.</param>
public sealed record IconFileSelection(string? FolderPath, IReadOnlyList<string> IconFiles)
{
    /// <summary>Copied text holding more lines than this is no list of paths.</summary>
    private const int MaximumPathLines = 1000;

    public static IconFileSelection None { get; } = new(null, []);

    /// <summary>Sorts paths into a folder and icon files; anything else (missing files, other kinds) is left out.</summary>
    public static IconFileSelection FromPaths(IEnumerable<string> paths)
    {
        var givenPaths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        return new IconFileSelection(givenPaths.FirstOrDefault(IsFolder), givenPaths.Where(IsIconFile).ToList());
    }

    /// <summary>
    /// The paths in copied text, one per line, as Everything's Copy Full Name and Explorer's Copy as Path give them: quotes are taken off and environment variables expanded.
    /// Lines that aren't a full path are skipped, so any other text gives none.
    /// </summary>
    public static IReadOnlyList<string> ParsePathText(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > MaximumPathLines)
        {
            return [];
        }
        var paths = new List<string>();
        foreach (var line in lines)
        {
            var path = Environment.ExpandEnvironmentVariables(line.Trim('"').Trim());
            if (path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.IsPathFullyQualified(path))
            {
                paths.Add(path);
            }
        }
        return paths;
    }

    private static bool IsFolder(string path)
    {
        return Directory.Exists(path) || ShortcutReader.ReadFolderTarget(path) is not null;
    }

    private static bool IsIconFile(string path)
    {
        return File.Exists(path) && (IconSource.GetKind(path) is not null || (ShortcutReader.IsShortcut(path) && ShortcutReader.ReadFolderTarget(path) is null));
    }
}

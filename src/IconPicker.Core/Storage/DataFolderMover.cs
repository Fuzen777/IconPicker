namespace IconPicker.Core.Storage;

/// <summary>
/// Moves the app's data (settings, saved icons, built icons, the error log) between %LocalAppData% and a portable Data folder next to the app.
/// It copies everything first and removes the old folder last, so a failure never leaves the data split: the folder the app reads from is either untouched or complete.
/// </summary>
public static class DataFolderMover
{
    /// <summary>The elevated helper's job files: only needed while it runs, so they are never moved.</summary>
    public const string JobsFolderName = "jobs";

    public const string ErrorLogFileName = "error.log";

    private const string PartialSuffix = ".partial";
    private const string RemovingSuffix = ".removing";

    /// <summary>
    /// Moves the contents of <paramref name="sourceFolder"/> into <paramref name="destinationFolder"/>, then deletes the source.
    /// A missing destination only appears once complete, which matters when its existence is what the app looks for (the portable Data folder).
    /// An existing destination is merged into: moved files replace files of the same name, and the error logs are joined.
    /// </summary>
    public static void Move(string sourceFolder, string destinationFolder)
    {
        if (Directory.Exists(destinationFolder))
        {
            CopyContents(sourceFolder, destinationFolder, isTopLevel: true);
        }
        else
        {
            var partialFolder = destinationFolder + PartialSuffix;
            DeleteFolder(partialFolder);
            Directory.CreateDirectory(partialFolder);
            try
            {
                CopyContents(sourceFolder, partialFolder, isTopLevel: true);
                Directory.Move(partialFolder, destinationFolder);
            }
            catch
            {
                DeleteFolder(partialFolder);
                throw;
            }
        }
        RemoveFolder(sourceFolder);
    }

    private static void CopyContents(string sourceFolder, string destinationFolder, bool isTopLevel)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(sourceFolder))
        {
            var fileName = Path.GetFileName(file);
            // Settings are written beside the real file and swapped in: a leftover is a write that never finished.
            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var destinationFile = Path.Combine(destinationFolder, fileName);
            if (isTopLevel && string.Equals(fileName, ErrorLogFileName, StringComparison.OrdinalIgnoreCase) && File.Exists(destinationFile))
            {
                File.AppendAllText(destinationFile, File.ReadAllText(file));
                continue;
            }
            // A hidden or read-only file (a saved icon's desktop.ini) can't be overwritten until its attributes are cleared; the copy brings them back.
            if (File.Exists(destinationFile))
            {
                File.SetAttributes(destinationFile, FileAttributes.Normal);
            }
            File.Copy(file, destinationFile, overwrite: true);
        }
        foreach (var subFolder in Directory.EnumerateDirectories(sourceFolder))
        {
            var folderName = Path.GetFileName(subFolder);
            if (isTopLevel && string.Equals(folderName, JobsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var destinationSubFolder = Path.Combine(destinationFolder, folderName);
            Directory.CreateDirectory(destinationSubFolder);
            CopyContents(subFolder, destinationSubFolder, isTopLevel: false);
            // Explorer only reads a folder's desktop.ini (a saved icon's own icon) when the folder is ReadOnly or System.
            var shellAttributes = File.GetAttributes(subFolder) & (FileAttributes.ReadOnly | FileAttributes.System);
            if (shellAttributes != 0)
            {
                File.SetAttributes(destinationSubFolder, File.GetAttributes(destinationSubFolder) | shellAttributes);
            }
        }
    }

    /// <summary>Renamed first, so a deletion that fails halfway leaves no folder under the old name for the app to read from.</summary>
    private static void RemoveFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }
        var removingFolder = folder + RemovingSuffix;
        DeleteFolder(removingFolder);
        Directory.Move(folder, removingFolder);
        try
        {
            DeleteFolder(removingFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The data is already in its new place; the leftover is cleared the next time a move runs.
        }
    }

    /// <summary>Deletes a folder and everything in it, read-only files and folders included.</summary>
    private static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        foreach (var subFolder in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(subFolder, FileAttributes.Directory);
        }
        Directory.Delete(folder, recursive: true);
    }
}

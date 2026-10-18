namespace IconPicker.Core.Folders;

public static class FileHelpers
{
    /// <summary>Deletes a file even when it is hidden, system or read-only.</summary>
    public static void DeleteHiddenFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }
    }

    /// <summary>Whether a path lies inside a folder (not the folder itself).</summary>
    public static bool IsInsideFolder(string fullPath, string folderPath)
    {
        var folderPrefix = Path.TrimEndingDirectorySeparator(folderPath) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The path relative to a folder when it lies inside it, otherwise the full path.</summary>
    public static string GetPathRelativeToFolder(string fullPath, string folderPath)
    {
        return IsInsideFolder(fullPath, folderPath) ? Path.GetRelativePath(folderPath, fullPath) : fullPath;
    }
}

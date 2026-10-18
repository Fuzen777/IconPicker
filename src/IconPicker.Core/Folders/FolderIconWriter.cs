using System.Globalization;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Folders;

/// <summary>What to write for a folder's new icon.</summary>
/// <param name="IconSetting">The IconResource value.</param>
/// <param name="IconFileBytes">The .ico to write as folder-icon.ico, or null for a link to an existing file.</param>
public sealed record IconWritePlan(string IconSetting, byte[]? IconFileBytes)
{
    public static IconWritePlan Copy(byte[] iconFileBytes)
    {
        return new IconWritePlan(FolderIconWriter.CopiedIconSetting, iconFileBytes);
    }

    public static IconWritePlan Link(string relativePath, int iconIndex)
    {
        return new IconWritePlan(relativePath + "," + iconIndex.ToString(CultureInfo.InvariantCulture), null);
    }
}

/// <summary>Sets and removes folder icons, ported from Set-FolderIcon and Remove-FolderIcon in Set Folder Icons.ps1.</summary>
public static class FolderIconWriter
{
    public const string IconFileName = "folder-icon.ico";

    /// <summary>Relative, so the icon survives the folder being moved or the drive letter changing. It is also how the app and the script recognise their own copies.</summary>
    public const string CopiedIconSetting = IconFileName + ",0";

    private const FileAttributes HiddenSystemAttributes = FileAttributes.Hidden | FileAttributes.System;

    /// <summary>Gives the folder its new icon. On failure the folder is put back as it was and the error is rethrown.</summary>
    public static void SetIcon(DirectoryInfo folder, IconWritePlan plan)
    {
        var snapshot = FolderIconSnapshot.Take(folder);
        try
        {
            var previousState = FolderIconState.Read(folder);
            var iconFilePath = Path.Combine(folder.FullName, IconFileName);
            if (plan.IconFileBytes is not null)
            {
                FileHelpers.DeleteHiddenFile(iconFilePath);
                File.WriteAllBytes(iconFilePath, plan.IconFileBytes);
                File.SetAttributes(iconFilePath, HiddenSystemAttributes);
            }

            var desktopIni = DesktopIniFile.Read(folder.FullName);
            desktopIni.SetIconResource(plan.IconSetting);
            desktopIni.Save();

            // A link replaces the copy, which nothing points to any more.
            if (plan.IconFileBytes is null && previousState.Kind == FolderIconKind.Copied)
            {
                FileHelpers.DeleteHiddenFile(iconFilePath);
            }

            // Explorer only reads desktop.ini in folders marked ReadOnly or System. On a folder, ReadOnly doesn't make the files inside read-only.
            folder.Refresh();
            folder.Attributes |= FileAttributes.ReadOnly;
        }
        catch
        {
            snapshot.TryRestore();
            throw;
        }
        ShellNotifier.NotifyItemChanged(folder.FullName);
    }

    /// <summary>
    /// Removes the folder's custom icon and keeps its other settings. The icon file is only deleted when it is the app's or the script's own copy. Returns whether anything changed.
    /// </summary>
    public static bool RemoveIcon(DirectoryInfo folder)
    {
        var snapshot = FolderIconSnapshot.Take(folder);
        try
        {
            var previousState = FolderIconState.Read(folder);
            if (previousState.Kind == FolderIconKind.Copied)
            {
                FileHelpers.DeleteHiddenFile(Path.Combine(folder.FullName, IconFileName));
            }
            var desktopIni = DesktopIniFile.Read(folder.FullName);
            if (!desktopIni.RemoveIconSettings())
            {
                return previousState.Kind == FolderIconKind.Copied;
            }
            if (desktopIni.HasContent)
            {
                desktopIni.Save();
            }
            else
            {
                desktopIni.Delete();
                // ReadOnly was only there for desktop.ini, which is gone.
                folder.Refresh();
                folder.Attributes &= ~FileAttributes.ReadOnly;
            }
        }
        catch
        {
            snapshot.TryRestore();
            throw;
        }
        ShellNotifier.NotifyItemChanged(folder.FullName);
        return true;
    }

    /// <summary>The files an icon change touches, saved so a failed change leaves no half-made icon behind.</summary>
    private sealed class FolderIconSnapshot
    {
        private readonly DirectoryInfo _folder;
        private readonly FileAttributes _folderAttributes;
        private readonly SavedFile _desktopIni;
        private readonly SavedFile _iconFile;

        private FolderIconSnapshot(DirectoryInfo folder)
        {
            _folder = folder;
            folder.Refresh();
            _folderAttributes = folder.Attributes;
            _desktopIni = SavedFile.Take(Path.Combine(folder.FullName, DesktopIniFile.FileName));
            _iconFile = SavedFile.Take(Path.Combine(folder.FullName, IconFileName));
        }

        public static FolderIconSnapshot Take(DirectoryInfo folder)
        {
            return new FolderIconSnapshot(folder);
        }

        public void TryRestore()
        {
            _desktopIni.TryRestore();
            _iconFile.TryRestore();
            try
            {
                _folder.Refresh();
                _folder.Attributes = _folderAttributes;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Restoring fails the same way when Windows refused the folder, and the first error is the one worth reporting.
            }
        }

        private sealed record SavedFile(string FilePath, byte[]? Content, FileAttributes Attributes)
        {
            public static SavedFile Take(string filePath)
            {
                return File.Exists(filePath) ? new SavedFile(filePath, File.ReadAllBytes(filePath), File.GetAttributes(filePath)) : new SavedFile(filePath, null, FileAttributes.Normal);
            }

            public void TryRestore()
            {
                try
                {
                    if (Content is null)
                    {
                        FileHelpers.DeleteHiddenFile(FilePath);
                        return;
                    }
                    if (File.Exists(FilePath))
                    {
                        File.SetAttributes(FilePath, FileAttributes.Normal);
                    }
                    File.WriteAllBytes(FilePath, Content);
                    File.SetAttributes(FilePath, Attributes);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Same as above: the original error is reported instead.
                }
            }
        }
    }
}

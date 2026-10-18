using System.Runtime.InteropServices;
using IconPicker.Core.Folders;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace IconPicker.Core.Shell;

/// <summary>
/// Gives a shortcut to a folder the folder's icon. Explorer doesn't reliably show a folder's icon on shortcuts made before it was set (they keep the plain folder), so the shortcut points at the icon file itself.
/// </summary>
public static class ShortcutIconWriter
{
    private const int AccessDenied = unchecked((int)0x80070005);

    /// <summary>Points the shortcut at the folder's working icon. Does nothing when the folder has none.</summary>
    public static void ShowFolderIcon(string shortcutPath, DirectoryInfo folder)
    {
        var state = FolderIconState.Read(folder);
        if (state is { HasCustomIcon: true, IsBroken: false, IconPath: { } iconPath })
        {
            Write(shortcutPath, iconPath, state.IconIndex);
        }
    }

    /// <summary>Takes off the shortcut's icon when it comes from inside the folder (the folder's icon, set by <see cref="ShowFolderIcon"/>), so it shows its target's again. An icon from anywhere else was set by hand and stays. Returns whether anything changed.</summary>
    public static bool RemoveFolderIcon(string shortcutPath, DirectoryInfo folder)
    {
        if (ShortcutReader.ReadOwnIcon(shortcutPath) is not { } icon || !FileHelpers.IsInsideFolder(icon.FilePath, folder.FullName))
        {
            return false;
        }
        Write(shortcutPath, "", 0);
        return true;
    }

    private static void Write(string shortcutPath, string iconPath, int iconIndex)
    {
        IShellLinkW? shellLink = null;
        try
        {
            shellLink = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(shortcutPath, STGM.STGM_READWRITE);
            shellLink.SetIconLocation(iconPath, iconIndex);
            persistFile.Save(shortcutPath, true);
        }
        catch (COMException exception) when (exception.HResult == AccessDenied)
        {
            throw new UnauthorizedAccessException(exception.Message, exception);
        }
        catch (COMException exception)
        {
            throw new IOException(exception.Message, exception);
        }
        finally
        {
            if (shellLink is not null)
            {
                Marshal.ReleaseComObject(shellLink);
            }
        }
        ShellNotifier.NotifyItemChanged(shortcutPath);
    }
}

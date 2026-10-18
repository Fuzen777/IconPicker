using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace IconPicker.Core.Shell;

/// <summary>Tells Explorer that folder and shortcut icons changed.</summary>
public static class ShellNotifier
{
    public static unsafe void NotifyItemChanged(string itemPath)
    {
        fixed (char* itemPathPointer = itemPath)
        {
            PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_UPDATEITEM, SHCNF_FLAGS.SHCNF_PATHW, itemPathPointer, null);
        }
    }

    /// <summary>Makes Explorer redraw icons it has already cached.</summary>
    public static unsafe void NotifyIconsChanged()
    {
        PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, SHCNF_FLAGS.SHCNF_IDLIST, null, null);
    }
}

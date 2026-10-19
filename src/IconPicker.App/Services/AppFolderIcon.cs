using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;

namespace IconPicker.App.Services;

/// <summary>
/// Gives the app's own folder the app's icon, the first time a release copy starts there: a zip can't carry the folder attribute Explorer needs to read
/// desktop.ini, so the app sets it itself. Once per location, so an icon removed or replaced by hand stays that way; a folder with an icon of its own is left alone.
/// </summary>
internal static class AppFolderIcon
{
#if DEBUG
    private static readonly bool IsReleaseBuild = false;
#else
    private static readonly bool IsReleaseBuild = true;
#endif

    /// <summary>Whether the app's folder should get its icon now: not yet tried there, and a release build (Debug builds run from the build output).</summary>
    public static bool IsDue(AppSettings settings)
    {
        return IsReleaseBuild && !string.Equals(settings.AppFolderIconPath, AppFolder.FullName, StringComparison.OrdinalIgnoreCase);
    }

    public static DirectoryInfo AppFolder { get; } = new(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));

    /// <summary>Sets the icon unless the folder already has one. A folder Windows won't let the app write to (such as Program Files) is left as it is.</summary>
    public static async Task ApplyAsync()
    {
        try
        {
            if (FolderIconState.Read(AppFolder).HasCustomIcon)
            {
                return;
            }
            var appIcon = new IconSource(Path.Combine(AppFolder.FullName, "Assets", "AppIcon.ico"), IconSourceKind.IconFile);
            await IconApplyService.ApplyAsync([new IconChange(AppFolder.FullName, IconChangeKind.Set, appIcon)], IconTarget.Copy).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(exception);
        }
    }
}

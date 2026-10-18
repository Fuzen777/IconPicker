using System.Globalization;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Folders;

public enum FolderIconKind
{
    /// <summary>The default folder icon.</summary>
    None,

    /// <summary>A copy made by the app or by Set Folder Icons.ps1: <c>folder-icon.ico</c> inside the folder.</summary>
    Copied,

    /// <summary>A link to a file inside the folder, such as <c>bin\Game.exe,0</c>.</summary>
    Linked,

    /// <summary>An icon file somewhere else, usually set by hand.</summary>
    External,
}

/// <summary>What a folder's desktop.ini says about its icon.</summary>
/// <param name="Kind">Where the icon comes from.</param>
/// <param name="IsBroken">The icon file is missing, or the folder lacks the attribute Explorer needs to read desktop.ini, so Explorer shows the default icon.</param>
/// <param name="IconSetting">The raw IconResource or IconFile value.</param>
/// <param name="IconPath">The icon file's full path.</param>
/// <param name="IconIndex">Which icon inside that file.</param>
/// <param name="IsMissingFromShortcut">Read through a shortcut to the folder: the folder has a working icon, but the shortcut doesn't point at it, so it may still show the plain folder.</param>
public sealed record FolderIconState(FolderIconKind Kind, bool IsBroken, string? IconSetting, string? IconPath, int IconIndex, bool IsMissingFromShortcut = false)
{
    public static readonly FolderIconState NoIcon = new(FolderIconKind.None, false, null, null, 0);

    public bool HasCustomIcon => Kind != FolderIconKind.None;

    /// <summary>The folder has no working icon, so it is proposed like an empty one, as the script does.</summary>
    public bool NeedsIcon => Kind == FolderIconKind.None || IsBroken || IsMissingFromShortcut;

    /// <summary>The folder's icon as a shortcut to it shows it, or the folder's own when <paramref name="shortcutPath"/> is null.</summary>
    public static FolderIconState Read(DirectoryInfo folder, string? shortcutPath)
    {
        var state = Read(folder);
        if (shortcutPath is null || state is not { HasCustomIcon: true, IsBroken: false, IconPath: { } iconPath })
        {
            return state;
        }
        var shortcutIcon = ShortcutReader.ReadOwnIcon(shortcutPath);
        var isShown = shortcutIcon is not null && string.Equals(shortcutIcon.FilePath, iconPath, StringComparison.OrdinalIgnoreCase) && shortcutIcon.IconIndex == state.IconIndex;
        return isShown ? state : state with { IsMissingFromShortcut = true };
    }

    public static FolderIconState Read(DirectoryInfo folder)
    {
        // A DirectoryInfo from a folder listing keeps the attributes it was listed with, which may predate the icon.
        folder.Refresh();
        var desktopIni = DesktopIniFile.Read(folder.FullName);
        var iconSetting = desktopIni.GetIconSetting();
        if (iconSetting is null)
        {
            return NoIcon;
        }
        var (iconFileText, iconIndex) = SplitIconSetting(iconSetting, desktopIni);
        var kind = string.Equals(iconSetting.Value, FolderIconWriter.CopiedIconSetting, StringComparison.OrdinalIgnoreCase) ? FolderIconKind.Copied : FolderIconKind.External;
        string? iconPath = null;
        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(iconFileText);
            iconPath = Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(folder.FullName, expandedPath));
            if (kind != FolderIconKind.Copied && FileHelpers.IsInsideFolder(iconPath, folder.FullName))
            {
                kind = FolderIconKind.Linked;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unreadable path leaves the icon broken.
        }
        // Explorer only reads desktop.ini in folders marked ReadOnly or System.
        var isReadByExplorer = (folder.Attributes & (FileAttributes.ReadOnly | FileAttributes.System)) != 0;
        var isBroken = iconPath is null || !File.Exists(iconPath) || !isReadByExplorer;
        return new FolderIconState(kind, isBroken, iconSetting.Value, iconPath, iconIndex);
    }

    /// <summary>IconResource holds "file,index"; the older IconFile holds the file alone, with IconIndex beside it.</summary>
    private static (string IconFile, int IconIndex) SplitIconSetting(DesktopIniSetting iconSetting, DesktopIniFile desktopIni)
    {
        var value = iconSetting.Value;
        if (string.Equals(iconSetting.Key, "IconFile", StringComparison.OrdinalIgnoreCase))
        {
            var iconIndexText = desktopIni.GetValue("IconIndex");
            var iconIndex = int.TryParse(iconIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex : 0;
            return (value.Trim().Trim('"'), iconIndex);
        }
        var commaIndex = value.LastIndexOf(',');
        if (commaIndex > 0 && int.TryParse(value[(commaIndex + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var resourceIndex))
        {
            return (value[..commaIndex].Trim().Trim('"'), resourceIndex);
        }
        return (value.Trim().Trim('"'), 0);
    }
}

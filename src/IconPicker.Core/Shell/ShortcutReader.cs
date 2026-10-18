using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace IconPicker.Core.Shell;

/// <summary>The file a shortcut takes its icon from, and which icon inside it.</summary>
/// <param name="FilePath">The icon file's full path.</param>
/// <param name="IconIndex">For an .exe or .dll, which of its icons, as in <c>IconResource=file,N</c> (a negative N is a resource ID).</param>
public sealed record ShortcutIcon(string FilePath, int IconIndex);

/// <summary>Reads which icon a shortcut (.lnk) or internet shortcut (.url) shows in Explorer.</summary>
public static class ShortcutReader
{
    private const int PathBufferLength = 1024;

    // SLGP_RAWPATH: the path as stored, environment variables included, without checking that it exists.
    private const uint RawPathFlag = 0x4;

    private static readonly HashSet<string> ShortcutExtensions = new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url" };

    static ShortcutReader()
    {
        // Internet shortcuts are written in the system's ANSI code page.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static bool IsShortcut(string filePath)
    {
        return ShortcutExtensions.Contains(Path.GetExtension(filePath));
    }

    /// <summary>
    /// The icon the shortcut shows: the one it was given, otherwise its target's first icon when the target is an .exe. Null when the shortcut can't be read, shows its target's file-type icon (a document, a folder, a web page), or its icon file is missing.
    /// </summary>
    public static ShortcutIcon? ReadIcon(string shortcutPath)
    {
        var storedIcon = string.Equals(Path.GetExtension(shortcutPath), ".url", StringComparison.OrdinalIgnoreCase)
            ? ReadInternetShortcutIcon(shortcutPath)
            : ReadLinkIcon(shortcutPath);
        if (storedIcon is not { } icon || string.IsNullOrWhiteSpace(icon.Path) || icon.Path.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }
        var iconPath = Environment.ExpandEnvironmentVariables(icon.Path.Trim().Trim('"'));
        try
        {
            iconPath = Path.GetFullPath(iconPath, Path.GetDirectoryName(Path.GetFullPath(shortcutPath))!);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
        return File.Exists(iconPath) ? new ShortcutIcon(iconPath, icon.Index) : null;
    }

    /// <summary>The folder a .lnk shortcut opens, or null when it opens anything else or its folder is missing. A drive doesn't count, since it can't take a folder icon.</summary>
    public static DirectoryInfo? ReadFolderTarget(string shortcutPath)
    {
        if (!string.Equals(Path.GetExtension(shortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var targetPath = UseShellLink(shortcutPath, shellLink => ReadTargetPath(shellLink));
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }
        try
        {
            var target = new DirectoryInfo(Path.GetFullPath(Environment.ExpandEnvironmentVariables(targetPath)));
            return target.Exists && target.Parent is not null ? target : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The icon a .lnk shortcut was given, made absolute, or null when it has none of its own (it then shows its target's).</summary>
    public static ShortcutIcon? ReadOwnIcon(string shortcutPath)
    {
        if (UseShellLink(shortcutPath, ReadIconLocation) is not { } icon || string.IsNullOrWhiteSpace(icon.Path))
        {
            return null;
        }
        try
        {
            var iconPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(icon.Path.Trim().Trim('"')), Path.GetDirectoryName(Path.GetFullPath(shortcutPath))!);
            return new ShortcutIcon(iconPath, icon.Index);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static (string Path, int Index)? ReadLinkIcon(string shortcutPath)
    {
        return UseShellLink(shortcutPath, shellLink =>
        {
            if (ReadIconLocation(shellLink) is { } icon)
            {
                return icon;
            }
            var targetPath = ReadTargetPath(shellLink);
            // Without an icon of its own, a shortcut shows its target's: an executable's first icon, or the file type's icon for anything else, which says nothing about the app.
            return string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase) ? (targetPath, 0) : ((string Path, int Index)?)null;
        });
    }

    private static (string Path, int Index)? ReadIconLocation(IShellLinkW shellLink)
    {
        Span<char> buffer = stackalloc char[PathBufferLength];
        shellLink.GetIconLocation(buffer, out var iconIndex);
        var iconPath = ReadBuffer(buffer);
        return iconPath.Length > 0 ? (iconPath, iconIndex) : null;
    }

    private static string ReadTargetPath(IShellLinkW shellLink)
    {
        Span<char> buffer = stackalloc char[PathBufferLength];
        var findData = default(WIN32_FIND_DATAW);
        shellLink.GetPath(buffer, ref findData, RawPathFlag);
        return ReadBuffer(buffer);
    }

    /// <summary>Loads a .lnk shortcut and reads it, or returns the default when it can't be read.</summary>
    private static T? UseShellLink<T>(string shortcutPath, Func<IShellLinkW, T?> read)
    {
        IShellLinkW? shellLink = null;
        try
        {
            shellLink = (IShellLinkW)new ShellLink();
            ((IPersistFile)shellLink).Load(shortcutPath, STGM.STGM_READ);
            return read(shellLink);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return default;
        }
        finally
        {
            if (shellLink is not null)
            {
                Marshal.ReleaseComObject(shellLink);
            }
        }
    }

    /// <summary>The IconFile and IconIndex of an internet shortcut's [InternetShortcut] section. Steam's game shortcuts point at the game's .ico in Steam's folder.</summary>
    private static (string Path, int Index)? ReadInternetShortcutIcon(string shortcutPath)
    {
        string[] lines;
        try
        {
            using var reader = new StreamReader(shortcutPath, Encoding.GetEncoding(0), detectEncodingFromByteOrderMarks: true);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        string? iconFile = null;
        var iconIndex = 0;
        var isInSection = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                isInSection = string.Equals(line, "[InternetShortcut]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var separatorIndex = line.IndexOf('=');
            if (!isInSection || separatorIndex < 0)
            {
                continue;
            }
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (string.Equals(key, "IconFile", StringComparison.OrdinalIgnoreCase))
            {
                iconFile = value;
            }
            else if (string.Equals(key, "IconIndex", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var parsedIndex))
            {
                iconIndex = parsedIndex;
            }
        }
        return iconFile is null ? null : (iconFile, iconIndex);
    }

    private static string ReadBuffer(ReadOnlySpan<char> buffer)
    {
        var length = buffer.IndexOf('\0');
        return (length < 0 ? buffer : buffer[..length]).ToString();
    }
}

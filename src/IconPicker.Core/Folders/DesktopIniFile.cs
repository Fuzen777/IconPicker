using System.Text;
using System.Text.RegularExpressions;

namespace IconPicker.Core.Folders;

/// <summary>A key and value read from the [.ShellClassInfo] section.</summary>
public sealed record DesktopIniSetting(string Key, string Value);

/// <summary>
/// A folder's desktop.ini, read and written the way Set Folder Icons.ps1 does: in the file's own encoding, keeping every line it doesn't touch.
/// </summary>
public sealed partial class DesktopIniFile
{
    public const string FileName = "desktop.ini";

    private const FileAttributes HiddenSystemAttributes = FileAttributes.Hidden | FileAttributes.System;

    private readonly List<string> _lines;

    static DesktopIniFile()
    {
        // desktop.ini files without a byte order mark use the system's ANSI code page, which .NET only knows with this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private DesktopIniFile(string filePath, List<string> lines, Encoding encoding)
    {
        FilePath = filePath;
        _lines = lines;
        Encoding = encoding;
    }

    public string FilePath { get; }

    public Encoding Encoding { get; }

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Whether any line has text, so the file is worth keeping.</summary>
    public bool HasContent => _lines.Exists(line => !string.IsNullOrWhiteSpace(line));

    public static DesktopIniFile Read(string folderPath)
    {
        var filePath = Path.Combine(folderPath, FileName);
        var lines = new List<string>();
        if (!File.Exists(filePath))
        {
            // Explorer writes later settings back in the file's own encoding, and UTF-16 keeps non-English text intact.
            return new DesktopIniFile(filePath, lines, Encoding.Unicode);
        }
        using var reader = new StreamReader(filePath, Encoding.GetEncoding(0), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        lines.AddRange(LineBreakPattern().Split(text));
        return new DesktopIniFile(filePath, lines, reader.CurrentEncoding);
    }

    /// <summary>The folder's custom icon setting (IconResource or IconFile), or null when it uses the default folder icon.</summary>
    public DesktopIniSetting? GetIconSetting()
    {
        var section = FindShellClassInfoSection();
        if (section is null)
        {
            return null;
        }
        for (var i = section.Value.HeaderIndex + 1; i < section.Value.EndIndex; i++)
        {
            var match = IconSettingPattern().Match(_lines[i]);
            if (match.Success)
            {
                return new DesktopIniSetting(match.Groups[1].Value, match.Groups[2].Value);
            }
        }
        return null;
    }

    /// <summary>A value from the [.ShellClassInfo] section, or null when it isn't there.</summary>
    public string? GetValue(string key)
    {
        var section = FindShellClassInfoSection();
        if (section is null)
        {
            return null;
        }
        for (var i = section.Value.HeaderIndex + 1; i < section.Value.EndIndex; i++)
        {
            var separatorIndex = _lines[i].IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex > 0 && string.Equals(_lines[i][..separatorIndex].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return _lines[i][(separatorIndex + 1)..].Trim();
            }
        }
        return null;
    }

    /// <summary>Replaces any icon setting with <c>IconResource=value</c>, creating the section when there is none.</summary>
    public void SetIconResource(string iconResourceValue)
    {
        var iconLine = "IconResource=" + iconResourceValue;
        var section = FindShellClassInfoSection();
        if (section is null)
        {
            _lines.InsertRange(0, ["[.ShellClassInfo]", iconLine]);
            return;
        }
        for (var i = section.Value.EndIndex - 1; i > section.Value.HeaderIndex; i--)
        {
            if (IconKeyPattern().IsMatch(_lines[i]))
            {
                _lines.RemoveAt(i);
            }
        }
        _lines.Insert(section.Value.HeaderIndex + 1, iconLine);
    }

    /// <summary>Removes every icon setting and keeps the other settings. Returns whether anything was removed.</summary>
    public bool RemoveIconSettings()
    {
        var section = FindShellClassInfoSection();
        if (section is null)
        {
            return false;
        }
        var lineCountBefore = _lines.Count;
        for (var i = section.Value.EndIndex - 1; i > section.Value.HeaderIndex; i--)
        {
            if (IconKeyPattern().IsMatch(_lines[i]))
            {
                _lines.RemoveAt(i);
            }
        }
        if (_lines.Count == lineCountBefore)
        {
            return false;
        }
        var remainingSection = FindShellClassInfoSection()!.Value;
        var sectionIsEmpty = true;
        for (var i = remainingSection.HeaderIndex + 1; i < remainingSection.EndIndex; i++)
        {
            if (!string.IsNullOrWhiteSpace(_lines[i]))
            {
                sectionIsEmpty = false;
            }
        }
        if (sectionIsEmpty)
        {
            _lines.RemoveRange(remainingSection.HeaderIndex, remainingSection.EndIndex - remainingSection.HeaderIndex);
        }
        return true;
    }

    public void Save()
    {
        while (_lines.Count > 0 && string.IsNullOrWhiteSpace(_lines[^1]))
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        // Hidden files can't be overwritten, so the attributes are cleared first and put back after.
        if (File.Exists(FilePath))
        {
            File.SetAttributes(FilePath, FileAttributes.Normal);
        }
        File.WriteAllText(FilePath, string.Join("\r\n", _lines) + "\r\n", Encoding);
        File.SetAttributes(FilePath, HiddenSystemAttributes);
    }

    public void Delete()
    {
        FileHelpers.DeleteHiddenFile(FilePath);
    }

    /// <summary>Where the [.ShellClassInfo] section starts and ends (exclusive), or null when there is none.</summary>
    private (int HeaderIndex, int EndIndex)? FindShellClassInfoSection()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            if (ShellClassInfoHeaderPattern().IsMatch(_lines[i]))
            {
                var sectionEnd = i + 1;
                while (sectionEnd < _lines.Count && !SectionHeaderPattern().IsMatch(_lines[sectionEnd]))
                {
                    sectionEnd++;
                }
                return (i, sectionEnd);
            }
        }
        return null;
    }

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex LineBreakPattern();

    [GeneratedRegex(@"^\s*\[\s*\.ShellClassInfo\s*\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShellClassInfoHeaderPattern();

    [GeneratedRegex(@"^\s*\[")]
    private static partial Regex SectionHeaderPattern();

    [GeneratedRegex(@"^\s*(IconResource|IconFile)\s*=\s*(.*\S)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IconSettingPattern();

    [GeneratedRegex(@"^\s*(IconResource|IconFile|IconIndex)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IconKeyPattern();
}

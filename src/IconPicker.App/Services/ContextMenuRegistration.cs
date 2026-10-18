using Microsoft.Win32;

namespace IconPicker.App.Services;

public enum ContextMenuState
{
    Off,
    On,

    /// <summary>The entry exists but opens another copy of the app (or only part of it exists).</summary>
    Elsewhere,
}

/// <summary>
/// The classic "Pick folder icon…" right-click entry on folders, folder backgrounds and drives, under HKEY_CURRENT_USER so no admin rights are needed. On Windows 11 it sits under "Show more options".
/// </summary>
internal sealed class ContextMenuRegistration
{
    private const string VerbName = "IconPicker";
    private static readonly string[] ShellKeyPaths = [@"Software\Classes\Directory\shell", @"Software\Classes\Directory\Background\shell", @"Software\Classes\Drive\shell"];

    private static string ExePath => Environment.ProcessPath!;

    // %V is the folder, the folder whose background was clicked, or the drive.
    private static string ExpectedCommand => $"\"{ExePath}\" \"%V\"";

    public ContextMenuState GetState()
    {
        var commands = ShellKeyPaths.Select(ReadCommand).ToList();
        if (commands.TrueForAll(command => command is null))
        {
            return ContextMenuState.Off;
        }
        return commands.TrueForAll(command => string.Equals(command, ExpectedCommand, StringComparison.OrdinalIgnoreCase)) ? ContextMenuState.On : ContextMenuState.Elsewhere;
    }

    /// <summary>The program the entry opens, as registered, or null when there is no entry.</summary>
    public string? GetRegisteredCommand()
    {
        return ShellKeyPaths.Select(ReadCommand).FirstOrDefault(command => command is not null);
    }

    /// <summary>Adds the entry, or points it at this copy of the app.</summary>
    public void Register()
    {
        foreach (var shellKeyPath in ShellKeyPaths)
        {
            using var verbKey = Registry.CurrentUser.CreateSubKey($@"{shellKeyPath}\{VerbName}");
            verbKey.SetValue("", Strings.Get("ContextMenuText"));
            verbKey.SetValue("Icon", ExePath + ",0");
            using var commandKey = verbKey.CreateSubKey("command");
            commandKey.SetValue("", ExpectedCommand);
        }
    }

    public void Unregister()
    {
        foreach (var shellKeyPath in ShellKeyPaths)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{shellKeyPath}\{VerbName}", throwOnMissingSubKey: false);
        }
    }

    private static string? ReadCommand(string shellKeyPath)
    {
        using var commandKey = Registry.CurrentUser.OpenSubKey($@"{shellKeyPath}\{VerbName}\command");
        return commandKey?.GetValue("") as string;
    }
}

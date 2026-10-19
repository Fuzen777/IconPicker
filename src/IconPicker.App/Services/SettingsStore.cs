using System.Text.Json;
using System.Text.Json.Serialization;
using IconPicker.Core.Applying;

namespace IconPicker.App.Services;

/// <summary>Where and how big the window was, in physical pixels.</summary>
internal sealed record WindowPlacement(int X, int Y, int Width, int Height, bool IsMaximized);

/// <summary>The app's light or dark look.</summary>
internal enum AppTheme
{
    /// <summary>Follows Windows' own app mode.</summary>
    System,
    Light,
    Dark,
}

internal sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public IconTarget IconTarget { get; set; } = IconTarget.Copy;

    /// <summary>Whether icons shown by shortcuts (.lnk, .url) inside a folder are listed and predicted.</summary>
    public bool IncludeShortcuts { get; set; } = true;

    public List<string> Libraries { get; set; } = [];

    /// <summary>The last colors used on built icons (#RRGGBB), newest first.</summary>
    public List<string> RecentColors { get; set; } = [];

    public WindowPlacement? WindowPlacement { get; set; }
}

/// <summary>The app's settings, kept in settings.json in the data folder (%LocalAppData%\Icon Picker, or Data next to the app when portable).</summary>
internal sealed class SettingsStore
{
    public SettingsStore()
    {
        Current = Load();
    }

    public event EventHandler? Changed;

    public static string DataFolder => DataLocation.Folder;

    private static string SettingsPath => Path.Combine(DataFolder, "settings.json");

    public AppSettings Current { get; }

    /// <summary>Applies a change, saves it and tells listeners.</summary>
    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsLibrary(string folderPath)
    {
        return Current.Libraries.Exists(library => IsSameFolder(library, folderPath));
    }

    public void AddLibrary(string folderPath)
    {
        if (!IsLibrary(folderPath))
        {
            Update(settings => settings.Libraries.Add(Path.TrimEndingDirectorySeparator(folderPath)));
        }
    }

    public void RemoveLibrary(string folderPath)
    {
        Update(settings => settings.Libraries.RemoveAll(library => IsSameFolder(library, folderPath)));
    }

    private static bool IsSameFolder(string firstPath, string secondPath)
    {
        return string.Equals(Path.TrimEndingDirectorySeparator(firstPath), Path.TrimEndingDirectorySeparator(secondPath), StringComparison.OrdinalIgnoreCase);
    }

    private static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // A damaged file is kept aside for inspection and the defaults are used.
            File.Copy(SettingsPath, SettingsPath + ".bak", overwrite: true);
            return new AppSettings();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(DataFolder);
        // Written beside the real file and swapped in, so a crash mid-write can't leave it half-written.
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.AppSettings));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

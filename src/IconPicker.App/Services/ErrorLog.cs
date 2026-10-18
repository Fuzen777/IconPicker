using System.Globalization;

namespace IconPicker.App.Services;

/// <summary>Keeps unexpected errors in error.log in the data folder.</summary>
internal static class ErrorLog
{
    public static string LogPath { get; } = Path.Combine(SettingsStore.DataFolder, "error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataFolder);
            File.AppendAllText(LogPath, $"[{DateTime.Now.ToString("u", CultureInfo.InvariantCulture)}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Nothing more can be done when the log itself can't be written.
        }
    }
}

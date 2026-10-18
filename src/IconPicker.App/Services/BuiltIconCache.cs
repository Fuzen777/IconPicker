namespace IconPicker.App.Services;

/// <summary>
/// Built icons used for a folder without being saved to My icons, kept in %LocalAppData%\Icon Picker\Built until the folder copies them as folder-icon.ico. Files older than a day are deleted at startup.
/// </summary>
internal sealed class BuiltIconCache
{
    private static readonly TimeSpan KeptFor = TimeSpan.FromDays(1);

    public static string Folder { get; } = Path.Combine(SettingsStore.DataFolder, "Built");

    /// <summary>Writes the icon and returns its path.</summary>
    public async Task<string> WriteAsync(byte[] iconFile)
    {
        Directory.CreateDirectory(Folder);
        var iconPath = Path.Combine(Folder, Guid.NewGuid().ToString("N")[..12] + ".ico");
        await File.WriteAllBytesAsync(iconPath, iconFile);
        return iconPath;
    }

    public static void DeleteOldIcons()
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }
        foreach (var iconFile in new DirectoryInfo(Folder).EnumerateFiles("*.ico"))
        {
            try
            {
                if (DateTime.UtcNow - iconFile.LastWriteTimeUtc > KeptFor)
                {
                    iconFile.Delete();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still open somewhere; it goes next time.
            }
        }
    }
}

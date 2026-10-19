using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;

namespace IconPicker.Core.Recent;

/// <summary>An icon applied to a folder lately, kept as the finished .ico the folder got.</summary>
/// <param name="Id">The hash of its .ico, which also names its file: the same icon reached from two files is kept once.</param>
/// <param name="Name">What its tile was called when it was applied: the file's name, a shortcut's, or a built icon's own name.</param>
/// <param name="SourcePath">The file it was taken from (the shortcut, for an icon a shortcut showed), or null for an icon built in the app.</param>
/// <param name="LastUsed">When it was last applied.</param>
public sealed record RecentIcon(string Id, string Name, string? SourcePath, DateTimeOffset LastUsed)
{
    [JsonIgnore]
    public bool IsBuilt => SourcePath is null;
}

/// <summary>
/// The last icons applied by hand, in %LocalAppData%\Icon Picker\Recent icons: each one's finished .ico, named after its hash, and recent.json listing them, most recent first.
/// The copies stay usable after their source moves or goes. A file dropped from the list stays until <see cref="DeleteUnlisted"/> runs at startup, so an icon already picked can still be applied.
/// </summary>
public sealed class RecentIconStore(string rootFolder)
{
    public const int Capacity = 10;
    private const string IndexFileName = "recent.json";
    private const string IconExtension = ".ico";
    private const int IdLength = 16;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string RootFolder { get; } = rootFolder;

    private string IndexPath => Path.Combine(RootFolder, IndexFileName);

    public string GetIconPath(RecentIcon recentIcon)
    {
        return Path.Combine(RootFolder, recentIcon.Id + IconExtension);
    }

    /// <summary>The recent icons, most recent first, leaving out any whose file is gone.</summary>
    public List<RecentIcon> List()
    {
        return ReadIndex().Where(recentIcon => File.Exists(GetIconPath(recentIcon))).ToList();
    }

    /// <summary>Keeps the candidate's icon as the most recent one, moving it to the top when it is already listed, and drops the oldest past <see cref="Capacity"/>.</summary>
    /// <exception cref="IconSourceException">The candidate's file can't give an icon.</exception>
    public async Task<RecentIcon> AddAsync(IconCandidate candidate, CancellationToken cancellationToken)
    {
        var iconFile = await IconTargetResolver.CreateIconFileAsync(candidate.Source, cancellationToken).ConfigureAwait(false);
        var id = Convert.ToHexStringLower(SHA256.HashData(iconFile))[..IdLength];
        var recentIcon = candidate.Recent is { } listed
            ? listed with { Id = id, LastUsed = DateTimeOffset.Now }
            : new RecentIcon(id, candidate.Built?.Name ?? Path.GetFileName(candidate.RelativePath), candidate.Built is null ? candidate.ShortcutPath ?? candidate.Source.FilePath : null, DateTimeOffset.Now);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(RootFolder);
            var iconPath = GetIconPath(recentIcon);
            if (!File.Exists(iconPath))
            {
                await WriteReplacingAsync(iconPath, iconFile).ConfigureAwait(false);
            }
            var index = ReadIndex();
            index.RemoveAll(other => other.Id == id);
            index.Insert(0, recentIcon);
            await WriteIndexAsync(index.Take(Capacity).ToList()).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
        return recentIcon;
    }

    /// <summary>Takes the icon off the list. Its file stays until the next start, in case it is picked somewhere.</summary>
    public async Task RemoveAsync(string id)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var index = ReadIndex();
            if (index.RemoveAll(recentIcon => recentIcon.Id == id) > 0)
            {
                await WriteIndexAsync(index).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Deletes the files the list no longer names. Run at startup, before any of them can be picked.</summary>
    public void DeleteUnlisted()
    {
        if (!Directory.Exists(RootFolder))
        {
            return;
        }
        var listedFileNames = ReadIndex().Select(recentIcon => recentIcon.Id + IconExtension).Append(IndexFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new DirectoryInfo(RootFolder).EnumerateFiles())
        {
            if (listedFileNames.Contains(file.Name))
            {
                continue;
            }
            try
            {
                file.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still open somewhere; it goes next time.
            }
        }
    }

    private static bool IsValidId(string? id)
    {
        return id is { Length: IdLength } && id.All(char.IsAsciiHexDigitLower);
    }

    /// <summary>The list as saved, without entries that don't name a file of their own. An unreadable list counts as empty.</summary>
    private List<RecentIcon> ReadIndex()
    {
        try
        {
            if (!File.Exists(IndexPath))
            {
                return [];
            }
            var index = JsonSerializer.Deserialize(File.ReadAllText(IndexPath), RecentIconJsonContext.Default.ListRecentIcon) ?? [];
            return index.Where(recentIcon => recentIcon is not null && IsValidId(recentIcon.Id) && !string.IsNullOrWhiteSpace(recentIcon.Name)).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }

    private Task WriteIndexAsync(List<RecentIcon> index)
    {
        return WriteReplacingAsync(IndexPath, JsonSerializer.SerializeToUtf8Bytes(index, RecentIconJsonContext.Default.ListRecentIcon));
    }

    /// <summary>Written beside the real file and swapped in, so a crash mid-write can't leave it half-written.</summary>
    private static async Task WriteReplacingAsync(string filePath, byte[] content)
    {
        var temporaryPath = filePath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, content).ConfigureAwait(false);
        File.Move(temporaryPath, filePath, overwrite: true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<RecentIcon>))]
internal sealed partial class RecentIconJsonContext : JsonSerializerContext;

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IconPicker.Core.Library;

public enum BuiltInIconStyle
{
    Regular,
    Filled,
}

/// <summary>One icon of the built-in set.</summary>
/// <param name="Id">Its file name stem, such as <c>folder_open</c>.</param>
/// <param name="Name">Its display name, such as "Folder Open".</param>
/// <param name="Description">What Microsoft says it stands for, often empty.</param>
/// <param name="Keywords">The metaphors Microsoft lists for it, for searching.</param>
/// <param name="Styles">The styles it comes in.</param>
public sealed record BuiltInIcon(string Id, string Name, string Description, IReadOnlyList<string> Keywords, IReadOnlyList<BuiltInIconStyle> Styles);

/// <summary>
/// The built-in icons: Microsoft's Fluent UI System Icons (MIT), shipped as one zip holding index.json and icons/&lt;id&gt;_&lt;style&gt;.svg, made by Scripts\Build-FluentIconLibrary.ps1. Safe to use from several threads.
/// </summary>
public sealed class FluentIconLibrary : IDisposable
{
    private const string IndexEntryName = "index.json";

    private readonly string _zipPath;
    private readonly Lazy<LoadedLibrary> _library;

    // Zip entries can't be read from several threads at once.
    private readonly Lock _entryLock = new();

    public FluentIconLibrary(string zipPath)
    {
        _zipPath = zipPath;
        _library = new Lazy<LoadedLibrary>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Every icon, by name. Empty when the zip is missing or damaged.</summary>
    public IReadOnlyList<BuiltInIcon> Icons => _library.Value.Icons;

    /// <summary>
    /// The icons in that style matching every word of the query, in their name, description or keywords. Names starting with the query come first, then names holding it, then the rest. An empty query gives every icon in that style.
    /// </summary>
    public List<BuiltInIcon> Search(string query, BuiltInIconStyle style)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var trimmedQuery = string.Join(' ', words);
        return Icons
            .Where(icon => icon.Styles.Contains(style) && words.All(word => Matches(icon, word)))
            .OrderBy(icon => icon.Name.StartsWith(trimmedQuery, StringComparison.CurrentCultureIgnoreCase) ? 0 : icon.Name.Contains(trimmedQuery, StringComparison.CurrentCultureIgnoreCase) ? 1 : 2)
            .ThenBy(icon => icon.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public BuiltInIcon? Find(string id)
    {
        return _library.Value.IconsById.GetValueOrDefault(id);
    }

    /// <summary>The icon's SVG, or null when it doesn't come in that style or the zip can't be read.</summary>
    public byte[]? GetSvg(string id, BuiltInIconStyle style)
    {
        var library = _library.Value;
        if (library.Archive is null || !library.Entries.TryGetValue(GetEntryName(id, style), out var entry))
        {
            return null;
        }
        try
        {
            lock (_entryLock)
            {
                using var entryStream = entry.Open();
                using var svgBytes = new MemoryStream();
                entryStream.CopyTo(svgBytes);
                return svgBytes.ToArray();
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_library.IsValueCreated)
        {
            _library.Value.Archive?.Dispose();
        }
    }

    internal static string GetEntryName(string id, BuiltInIconStyle style)
    {
        return $"icons/{id}_{style.ToString().ToLowerInvariant()}.svg";
    }

    private static bool Matches(BuiltInIcon icon, string word)
    {
        return icon.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase)
            || icon.Description.Contains(word, StringComparison.CurrentCultureIgnoreCase)
            || icon.Keywords.Any(keyword => keyword.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }

    private LoadedLibrary Load()
    {
        ZipArchive? archive = null;
        try
        {
            archive = ZipFile.OpenRead(_zipPath);
            // Some zip writers separate folders with backslashes.
            var entries = archive.Entries.ToDictionary(entry => entry.FullName.Replace('\\', '/'), StringComparer.Ordinal);
            if (!entries.TryGetValue(IndexEntryName, out var indexEntry))
            {
                archive.Dispose();
                return LoadedLibrary.Empty;
            }
            BuiltInIconIndex? index;
            using (var indexStream = indexEntry.Open())
            {
                index = JsonSerializer.Deserialize(indexStream, BuiltInIconJsonContext.Default.BuiltInIconIndex);
            }
            var icons = (index?.Icons ?? [])
                .Select(icon => icon with { Styles = icon.Styles.Where(style => entries.ContainsKey(GetEntryName(icon.Id, style))).ToList() })
                .Where(icon => icon.Styles.Count > 0)
                .OrderBy(icon => icon.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return new LoadedLibrary(archive, entries, icons, icons.ToDictionary(icon => icon.Id, StringComparer.Ordinal));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or NotSupportedException)
        {
            archive?.Dispose();
            return LoadedLibrary.Empty;
        }
    }

    private sealed record LoadedLibrary(ZipArchive? Archive, Dictionary<string, ZipArchiveEntry> Entries, IReadOnlyList<BuiltInIcon> Icons, Dictionary<string, BuiltInIcon> IconsById)
    {
        public static LoadedLibrary Empty { get; } = new(null, [], [], []);
    }
}

/// <summary>index.json in the built-in icons zip.</summary>
internal sealed record BuiltInIconIndex(IReadOnlyList<BuiltInIcon> Icons);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(BuiltInIconIndex))]
internal sealed partial class BuiltInIconJsonContext : JsonSerializerContext;

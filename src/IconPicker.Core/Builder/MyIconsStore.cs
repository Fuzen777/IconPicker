using System.Text.Json;
using IconPicker.Core.Candidates;

namespace IconPicker.Core.Builder;

/// <summary>A built icon kept in My icons: its recipe, the source it was built from and the finished icon, in a folder of their own.</summary>
public sealed record SavedIcon(string Id, string FolderPath, IconRecipe Recipe)
{
    public string IconPath => Path.Combine(FolderPath, MyIconsStore.IconFileName);

    public string PreviewPath => Path.Combine(FolderPath, MyIconsStore.PreviewFileName);

    public string SourcePath => Path.Combine(FolderPath, Recipe.SourceFileName);

    /// <summary>The glyph again, from the copy of the source, to edit the icon. Null when the copy can't be read.</summary>
    public GlyphSource? LoadGlyphSource()
    {
        var kind = IconSource.GetKind(SourcePath);
        return kind is null ? null : GlyphSource.FromFile(new IconSource(SourcePath, kind.Value)) is { } source
            ? source with { Name = Recipe.Name, BuiltInIconId = Recipe.BuiltInIconId, BuiltInIconStyle = Recipe.BuiltInIconStyle }
            : null;
    }
}

/// <summary>
/// The icons saved to My icons, in %LocalAppData%\Icon Picker\My icons: one folder each, holding recipe.json, a copy of the source, the finished icon.ico and a preview.png. The copy makes every icon self-contained: moving or deleting the original file never breaks it.
/// </summary>
public sealed class MyIconsStore(string rootFolder)
{
    public const string RecipeFileName = "recipe.json";
    public const string IconFileName = "icon.ico";
    public const string PreviewFileName = "preview.png";
    private const string SourceFileStem = "source";
    private const string UnfinishedSuffix = ".saving";

    public string RootFolder { get; } = rootFolder;

    /// <summary>Raised after any change, so lists can refresh.</summary>
    public event EventHandler? Changed;

    /// <summary>The saved icons, most recently used first. Folders without a readable recipe or icon are skipped, and listed in <paramref name="skippedFolders"/> when given.</summary>
    public List<SavedIcon> List(ICollection<string>? skippedFolders = null)
    {
        if (!Directory.Exists(RootFolder))
        {
            return [];
        }
        var savedIcons = new List<SavedIcon>();
        foreach (var folder in Directory.EnumerateDirectories(RootFolder).Where(folder => !folder.EndsWith(UnfinishedSuffix, StringComparison.Ordinal)))
        {
            if (Read(folder) is { } savedIcon)
            {
                savedIcons.Add(savedIcon);
            }
            else
            {
                skippedFolders?.Add(folder);
            }
        }
        return savedIcons.OrderByDescending(savedIcon => savedIcon.Recipe.LastUsed).ThenBy(savedIcon => savedIcon.Recipe.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The saved icon with this id, or null when there is none.</summary>
    public SavedIcon? Find(string id)
    {
        return IsValidId(id) ? Read(Path.Combine(RootFolder, id)) : null;
    }

    /// <summary>The saved icon whose icon.ico this is, or null when the file isn't one.</summary>
    public SavedIcon? FindByIconPath(string iconPath)
    {
        var fullPath = Path.GetFullPath(iconPath);
        return string.Equals(Path.GetFileName(fullPath), IconFileName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(fullPath)), Path.GetFullPath(RootFolder), StringComparison.OrdinalIgnoreCase)
            ? Find(Path.GetFileName(Path.GetDirectoryName(fullPath)!))
            : null;
    }

    /// <summary>Saves a new icon. Everything is written to a folder of its own before it appears in the list, so an interrupted save leaves nothing behind.</summary>
    public async Task<SavedIcon> SaveAsync(GlyphSource source, IconStyle style, string name, BuiltIcon builtIcon)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var now = DateTimeOffset.Now;
        var recipe = new IconRecipe
        {
            Name = name,
            SourceFileName = SourceFileStem + source.FileExtension,
            BuiltInIconId = source.BuiltInIconId,
            BuiltInIconStyle = source.BuiltInIconStyle,
            Style = style,
            Created = now,
            LastUsed = now,
        };
        var finalFolder = Path.Combine(RootFolder, id);
        await WriteNewFolderAsync(finalFolder, async unfinishedFolder =>
        {
            await File.WriteAllBytesAsync(Path.Combine(unfinishedFolder, recipe.SourceFileName), source.FileBytes).ConfigureAwait(false);
            await WriteIconFilesAsync(unfinishedFolder, builtIcon).ConfigureAwait(false);
            await WriteRecipeAsync(unfinishedFolder, recipe).ConfigureAwait(false);
        }).ConfigureAwait(false);
        RaiseChanged();
        return new SavedIcon(id, finalFolder, recipe);
    }

    /// <summary>Saves a saved icon's new look, and its new source when it was given another glyph. Folders already using it keep their own copy until it is applied again.</summary>
    public async Task<SavedIcon> UpdateAsync(SavedIcon savedIcon, GlyphSource source, IconStyle style, string name, BuiltIcon builtIcon)
    {
        var recipe = savedIcon.Recipe with
        {
            Style = style,
            Name = name,
            LastUsed = DateTimeOffset.Now,
            SourceFileName = SourceFileStem + source.FileExtension,
            BuiltInIconId = source.BuiltInIconId,
            BuiltInIconStyle = source.BuiltInIconStyle,
        };
        await WriteReplacingAsync(Path.Combine(savedIcon.FolderPath, recipe.SourceFileName), source.FileBytes).ConfigureAwait(false);
        await WriteIconFilesAsync(savedIcon.FolderPath, builtIcon).ConfigureAwait(false);
        await WriteRecipeAsync(savedIcon.FolderPath, recipe).ConfigureAwait(false);
        if (!string.Equals(savedIcon.Recipe.SourceFileName, recipe.SourceFileName, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(savedIcon.SourcePath);
        }
        RaiseChanged();
        return savedIcon with { Recipe = recipe };
    }

    public async Task<SavedIcon> RenameAsync(SavedIcon savedIcon, string name)
    {
        var recipe = savedIcon.Recipe with { Name = name };
        await WriteRecipeAsync(savedIcon.FolderPath, recipe).ConfigureAwait(false);
        RaiseChanged();
        return savedIcon with { Recipe = recipe };
    }

    /// <summary>A copy under a new name, used just now so it shows first.</summary>
    public async Task<SavedIcon> DuplicateAsync(SavedIcon savedIcon, string name)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var finalFolder = Path.Combine(RootFolder, id);
        var now = DateTimeOffset.Now;
        var recipe = savedIcon.Recipe with { Name = name, Created = now, LastUsed = now };
        await WriteNewFolderAsync(finalFolder, async unfinishedFolder =>
        {
            foreach (var fileName in new[] { savedIcon.Recipe.SourceFileName, IconFileName, PreviewFileName })
            {
                var sourcePath = Path.Combine(savedIcon.FolderPath, fileName);
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, Path.Combine(unfinishedFolder, fileName));
                }
            }
            await WriteRecipeAsync(unfinishedFolder, recipe).ConfigureAwait(false);
        }).ConfigureAwait(false);
        RaiseChanged();
        return new SavedIcon(id, finalFolder, recipe);
    }

    public void Delete(SavedIcon savedIcon)
    {
        if (Directory.Exists(savedIcon.FolderPath))
        {
            Directory.Delete(savedIcon.FolderPath, recursive: true);
        }
        RaiseChanged();
    }

    /// <summary>Moves the icon to the top of the list, as it was just picked for a folder.</summary>
    public async Task<SavedIcon> MarkUsedAsync(SavedIcon savedIcon)
    {
        var recipe = savedIcon.Recipe with { LastUsed = DateTimeOffset.Now };
        await WriteRecipeAsync(savedIcon.FolderPath, recipe).ConfigureAwait(false);
        RaiseChanged();
        return savedIcon with { Recipe = recipe };
    }

    /// <summary>Writes a new icon's folder under a temporary name and moves it into place, so an interrupted save leaves nothing behind.</summary>
    private static async Task WriteNewFolderAsync(string finalFolder, Func<string, Task> writeFiles)
    {
        var unfinishedFolder = finalFolder + UnfinishedSuffix;
        Directory.CreateDirectory(unfinishedFolder);
        try
        {
            await writeFiles(unfinishedFolder).ConfigureAwait(false);
            Directory.Move(unfinishedFolder, finalFolder);
        }
        catch
        {
            try
            {
                Directory.Delete(unfinishedFolder, recursive: true);
            }
            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
            {
                // Left for List to skip; the original failure matters more.
            }
            throw;
        }
    }

    private static bool IsValidId(string id)
    {
        return id.Length > 0 && id.All(char.IsAsciiLetterOrDigit);
    }

    private static SavedIcon? Read(string folder)
    {
        try
        {
            var recipe = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(folder, RecipeFileName)), IconRecipeJsonContext.Default.IconRecipe);
            if (recipe is null || recipe.SourceFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !File.Exists(Path.Combine(folder, IconFileName)))
            {
                return null;
            }
            return new SavedIcon(Path.GetFileName(folder), folder, recipe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task WriteIconFilesAsync(string folder, BuiltIcon builtIcon)
    {
        await WriteReplacingAsync(Path.Combine(folder, IconFileName), builtIcon.IconFile).ConfigureAwait(false);
        await WriteReplacingAsync(Path.Combine(folder, PreviewFileName), builtIcon.PreviewPng).ConfigureAwait(false);
    }

    private static Task WriteRecipeAsync(string folder, IconRecipe recipe)
    {
        return WriteReplacingAsync(Path.Combine(folder, RecipeFileName), JsonSerializer.SerializeToUtf8Bytes(recipe, IconRecipeJsonContext.Default.IconRecipe));
    }

    /// <summary>Written beside the real file and swapped in, so a crash mid-write can't leave it half-written.</summary>
    private static async Task WriteReplacingAsync(string filePath, byte[] content)
    {
        var temporaryPath = filePath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, content).ConfigureAwait(false);
        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

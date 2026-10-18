using System.IO.Compression;
using System.Text;
using System.Text.Json;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Library;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class BuilderTests
{
    private const int Size = 256;
    private const string RedHex = "#FF0000";
    private const string GreenHex = "#00FF00";

    /// <summary>A black square filling its viewBox: the glyph covers exactly its share of the icon.</summary>
    private static readonly byte[] SquareGlyphSvg = Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><rect width="24" height="24"/></svg>""");

    [TestMethod]
    public void IconRecipe_RoundTripsThroughJson()
    {
        var recipe = new IconRecipe
        {
            Name = "Music",
            SourceFileName = "source.svg",
            BuiltInIconId = "music_note_2",
            BuiltInIconStyle = BuiltInIconStyle.Filled,
            Style = new IconStyle(GreenHex, IconShape.Rounded, RedHex, 0.6),
            Created = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.FromHours(2)),
            LastUsed = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.FromHours(2)),
        };

        var json = JsonSerializer.Serialize(recipe, IconRecipeJsonContext.Default.IconRecipe);

        Assert.Contains("\"Rounded\"", json);
        Assert.AreEqual(recipe, JsonSerializer.Deserialize(json, IconRecipeJsonContext.Default.IconRecipe));
    }

    [TestMethod]
    public void IconStyle_DefaultsAndClampsTheScale()
    {
        Assert.AreEqual(1.0, IconStyle.DefaultScale(IconShape.None));
        Assert.AreEqual(0.6, IconStyle.DefaultScale(IconShape.Circle));
        Assert.AreEqual(0.55, IconStyle.DefaultScale(IconShape.Folder));
        Assert.AreEqual(IconStyle.MinimumScale, (IconStyle.Default with { IconScale = 0.1 }).ClampedScale);
        Assert.AreEqual(IconStyle.MaximumScale, (IconStyle.Default with { IconScale = double.NaN }).ClampedScale);
        Assert.IsFalse((IconStyle.Default with { Shape = IconShape.Folder }).UsesBackgroundColor);
        Assert.IsTrue((IconStyle.Default with { Shape = IconShape.Square }).UsesBackgroundColor);
    }

    [TestMethod]
    public async Task ComposeAsync_Circle_HasTransparentCornersAndARecoloredGlyph()
    {
        var pixels = await IconComposer.ComposeAsync(SquareGlyph(), new IconStyle(GreenHex, IconShape.Circle, RedHex, 0.6), Size);

        Assert.AreEqual(0, PixelAt(pixels!, 4, 4).Alpha);
        Assert.AreEqual((255, 0, 0, 255), PixelAt(pixels!, 128, 30));
        Assert.AreEqual((0, 255, 0, 255), PixelAt(pixels!, 128, 128));
    }

    [TestMethod]
    public async Task ComposeAsync_Square_FillsToItsMargin()
    {
        var pixels = await IconComposer.ComposeAsync(SquareGlyph(), new IconStyle(null, IconShape.Square, RedHex, 0.6), Size);

        Assert.AreEqual(0, PixelAt(pixels!, 2, 2).Alpha);
        Assert.AreEqual((255, 0, 0, 255), PixelAt(pixels!, 14, 14));
        // No color asked: the glyph keeps its own black.
        Assert.AreEqual((0, 0, 0, 255), PixelAt(pixels!, 128, 128));
    }

    [TestMethod]
    public async Task ComposeAsync_Folder_DrawsWindowsFolderAndIgnoresTheBackgroundColor()
    {
        var pixels = await IconComposer.ComposeAsync(SquareGlyph(), new IconStyle(GreenHex, IconShape.Folder, RedHex, 0.3), Size);

        var visibleOutsideGlyph = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var pixel = PixelAt(pixels!, x, y);
                Assert.AreNotEqual((255, 0, 0, 255), pixel);
                if (pixel.Alpha > 0 && pixel != (0, 255, 0, 255))
                {
                    visibleOutsideGlyph++;
                }
            }
        }
        Assert.IsGreaterThan(Size * Size / 4, visibleOutsideGlyph);
        // The emblem sits on the folder's front, below the middle.
        Assert.AreEqual((0, 255, 0, 255), PixelAt(pixels!, 128, (int)(Size * 0.58)));
    }

    [TestMethod]
    public async Task ComposeAsync_SmallRasterIcon_IsEnlargedSmoothly()
    {
        using var testFolder = new TestFolder();
        // 32 px, left half red and right half blue.
        var halves = new byte[32 * 32 * 4];
        for (var offset = 0; offset < halves.Length; offset += 4)
        {
            var isLeft = offset / 4 % 32 < 16;
            halves[offset] = isLeft ? (byte)0 : (byte)255;
            halves[offset + 2] = isLeft ? (byte)255 : (byte)0;
            halves[offset + 3] = 255;
        }
        var iconFile = testFolder.CreateFile("small.ico", IcoFileBuilder.Build([IcoImage.FromPng(32, await ImageIconConverter.EncodePngAsync(halves, 32, 32))]));
        var glyph = new RasterGlyph(new IconSource(iconFile.FullName, IconSourceKind.IconFile));

        var pixels = await IconComposer.ComposeAsync(glyph, new IconStyle(null, IconShape.None, RedHex, 1.0), Size);

        Assert.AreEqual((255, 0, 0, 255), PixelAt(pixels!, 10, 128));
        Assert.AreEqual((0, 0, 255, 255), PixelAt(pixels!, 245, 128));
        // Enlarged with smoothing, the edge between the halves blends instead of jumping in one step.
        var blended = Enumerable.Range(118, 20).Select(x => PixelAt(pixels!, x, 128)).Count(pixel => pixel.Red is > 30 and < 225 && pixel.Blue is > 30 and < 225);
        Assert.IsGreaterThanOrEqualTo(2, blended);
    }

    [TestMethod]
    public async Task BuildAsync_GivesEverySizeAndAPreview()
    {
        var builtIcon = await IconComposer.BuildAsync(SquareGlyph(), new IconStyle(GreenHex, IconShape.Rounded, RedHex, 0.6));

        CollectionAssert.AreEqual(new[] { (256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16) }, IcoFileBuilder.ReadImageSizes(builtIcon!.IconFile)!.ToArray());
        var (_, width, height) = await TestImages.DecodePngAsync(builtIcon.PreviewPng);
        Assert.AreEqual((256, 256), (width, height));
    }

    [TestMethod]
    public async Task BuildAsync_GlyphThatCantBeDrawn_GivesNull()
    {
        using var testFolder = new TestFolder();
        var fake = testFolder.CreateTextFile("fake.png", "not a picture");

        Assert.IsNull(await IconComposer.BuildAsync(new RasterGlyph(new IconSource(fake.FullName, IconSourceKind.Image)), IconStyle.Default));
    }

    [TestMethod]
    public void GlyphSource_FromFile_KeepsOnlyWhatIsNeeded()
    {
        using var testFolder = new TestFolder();
        var svgFile = testFolder.CreateTextFile("Logo.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" onload="x()"><rect width="10" height="10"/></svg>""");

        var vector = GlyphSource.FromFile(new IconSource(svgFile.FullName, IconSourceKind.Vector))!;
        var executable = GlyphSource.FromFile(new IconSource(TestFolder.IconExePath, IconSourceKind.Module))!;

        Assert.AreEqual(("Logo", ".svg"), (vector.Name, vector.FileExtension));
        Assert.DoesNotContain("onload", Encoding.UTF8.GetString(vector.FileBytes));
        Assert.IsTrue(vector.Glyph.CanRecolor);
        // An executable's icon is kept as an .ico file, not the whole executable.
        Assert.AreEqual(".ico", executable.FileExtension);
        Assert.IsNotNull(IcoFileBuilder.ReadImageSizes(executable.FileBytes));
        Assert.IsFalse(executable.Glyph.CanRecolor);
        Assert.IsNull(GlyphSource.FromFile(new IconSource(Path.Combine(testFolder.Root.FullName, "missing.png"), IconSourceKind.Image)));
    }

    [TestMethod]
    public async Task MyIconsStore_SavesListsAndEditsIcons()
    {
        using var testFolder = new TestFolder();
        var store = new MyIconsStore(Path.Combine(testFolder.Root.FullName, "My icons"));
        var changes = 0;
        store.Changed += (_, _) => changes++;
        var source = BuiltInSource();
        var style = new IconStyle(GreenHex, IconShape.Circle, RedHex, 0.6);

        var music = await store.SaveAsync(source, style, "Music", (await IconComposer.BuildAsync(source.Glyph, style))!);
        var copy = await store.DuplicateAsync(music, "Music (copy)");
        music = await store.MarkUsedAsync(music);
        var renamed = await store.RenameAsync(copy, "Songs");
        var updated = await store.UpdateAsync(music, source, style with { Shape = IconShape.Square }, "Music", (await IconComposer.BuildAsync(source.Glyph, style with { Shape = IconShape.Square }))!);

        var listed = store.List();
        CollectionAssert.AreEqual(new[] { "Music", "Songs" }, listed.Select(icon => icon.Recipe.Name).ToArray());
        Assert.AreEqual(IconShape.Square, listed[0].Recipe.Style.Shape);
        Assert.IsTrue(File.Exists(updated.IconPath) && File.Exists(updated.PreviewPath) && File.Exists(updated.SourcePath));
        Assert.AreEqual(renamed.Id, store.FindByIconPath(renamed.IconPath)!.Id);
        Assert.IsNull(store.FindByIconPath(Path.Combine(testFolder.Root.FullName, "icon.ico")));
        Assert.AreEqual(5, changes);

        store.Delete(renamed);

        Assert.HasCount(1, store.List());
        Assert.IsFalse(Directory.Exists(renamed.FolderPath));
    }

    [TestMethod]
    public async Task MyIconsStore_UpdateWithAnotherGlyph_ReplacesTheSource()
    {
        using var testFolder = new TestFolder();
        var store = new MyIconsStore(Path.Combine(testFolder.Root.FullName, "My icons"));
        var builtInSource = BuiltInSource();
        var saved = await store.SaveAsync(builtInSource, IconStyle.Default, "Folder", (await IconComposer.BuildAsync(builtInSource.Glyph, IconStyle.Default))!);
        var picture = testFolder.CreateFile("photo.png", await TestImages.CreatePngAsync(64, 64));
        var pictureSource = GlyphSource.FromFile(new IconSource(picture.FullName, IconSourceKind.Image))!;

        var updated = await store.UpdateAsync(saved, pictureSource, IconStyle.Default, "Photo", (await IconComposer.BuildAsync(pictureSource.Glyph, IconStyle.Default))!);

        Assert.AreEqual("source.png", updated.Recipe.SourceFileName);
        Assert.IsNull(updated.Recipe.BuiltInIconId);
        Assert.IsTrue(File.Exists(updated.SourcePath));
        Assert.IsFalse(File.Exists(Path.Combine(saved.FolderPath, "source.svg")));
        Assert.IsInstanceOfType<RasterGlyph>(store.Find(saved.Id)!.LoadGlyphSource()!.Glyph);
    }

    [TestMethod]
    public async Task MyIconsStore_FailedDuplicate_LeavesNothingBehind()
    {
        using var testFolder = new TestFolder();
        var store = new MyIconsStore(Path.Combine(testFolder.Root.FullName, "My icons"));
        var source = BuiltInSource();
        var saved = await store.SaveAsync(source, IconStyle.Default, "Folder", (await IconComposer.BuildAsync(source.Glyph, IconStyle.Default))!);
        // A folder of the duplicate's name can't exist yet; the source copy is made unreadable instead.
        using (File.Open(saved.SourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => store.DuplicateAsync(saved, "Copy"));
        }

        CollectionAssert.AreEqual(new[] { saved.Id }, Directory.GetDirectories(store.RootFolder).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public async Task MyIconsStore_SkipsDamagedFoldersAndEditsWithoutTheBuiltInSet()
    {
        using var testFolder = new TestFolder();
        var store = new MyIconsStore(Path.Combine(testFolder.Root.FullName, "My icons"));
        var source = BuiltInSource();
        var saved = await store.SaveAsync(source, IconStyle.Default, "Folder", (await IconComposer.BuildAsync(source.Glyph, IconStyle.Default))!);
        testFolder.CreateTextFile(@"My icons\damaged\recipe.json", "{ not json");
        testFolder.CreateTextFile(@"My icons\noicon\recipe.json", File.ReadAllText(Path.Combine(saved.FolderPath, MyIconsStore.RecipeFileName)));
        var skipped = new List<string>();

        var listed = store.List(skipped);

        Assert.HasCount(1, listed);
        Assert.HasCount(2, skipped);
        // The recipe holds its own copy of the SVG, so it edits with no built-in set at hand.
        var reloaded = listed[0].LoadGlyphSource()!;
        Assert.IsInstanceOfType<VectorGlyph>(reloaded.Glyph);
        Assert.AreEqual(("folder", BuiltInIconStyle.Regular, "Folder"), (reloaded.BuiltInIconId, reloaded.BuiltInIconStyle, reloaded.Name));
    }

    [TestMethod]
    public void FluentIconLibrary_SearchesNamesDescriptionsAndKeywords()
    {
        using var testFolder = new TestFolder();
        using var library = new FluentIconLibrary(CreateTestLibrary(testFolder));

        Assert.HasCount(2, library.Icons);
        CollectionAssert.AreEqual(new[] { "Folder", "Folder Open" }, library.Search("fold", BuiltInIconStyle.Regular).Select(icon => icon.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "Folder" }, library.Search("fold", BuiltInIconStyle.Filled).Select(icon => icon.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "Folder" }, library.Search("COLLECTION", BuiltInIconStyle.Regular).Select(icon => icon.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "Folder Open" }, library.Search("open files", BuiltInIconStyle.Regular).Select(icon => icon.Name).ToArray());
        Assert.HasCount(2, library.Search("  ", BuiltInIconStyle.Regular));
        Assert.IsNotNull(library.GetSvg("folder", BuiltInIconStyle.Filled));
        Assert.IsNull(library.GetSvg("folder_open", BuiltInIconStyle.Filled));
        Assert.IsNull(library.Find("ghost"));
    }

    [TestMethod]
    public async Task FluentIconLibrary_ReadsSafelyFromManyThreads()
    {
        using var testFolder = new TestFolder();
        using var library = new FluentIconLibrary(CreateTestLibrary(testFolder));

        var svgs = await Task.WhenAll(Enumerable.Range(0, 200).Select(index => Task.Run(() => library.GetSvg(index % 2 == 0 ? "folder" : "folder_open", BuiltInIconStyle.Regular))));

        Assert.IsTrue(svgs.All(svg => svg is not null && SvgFile.TryParse(svg) is not null));
    }

    [TestMethod]
    public void FluentIconLibrary_MissingZip_IsEmpty()
    {
        using var library = new FluentIconLibrary(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip"));

        Assert.IsEmpty(library.Icons);
        Assert.IsNull(library.GetSvg("folder", BuiltInIconStyle.Regular));
    }

    [TestMethod]
    public void FluentIconLibrary_ShippedSet_HasEveryIconItLists()
    {
        using var library = new FluentIconLibrary(Path.Combine(AppContext.BaseDirectory, "Assets", "FluentIcons.zip"));

        Assert.IsGreaterThan(2000, library.Icons.Count);
        foreach (var icon in library.Icons)
        {
            foreach (var style in icon.Styles)
            {
                Assert.IsNotNull(SvgFile.TryParse(library.GetSvg(icon.Id, style)!), $"{icon.Id} {style}");
            }
        }
        Assert.IsNotEmpty(library.Search("music", BuiltInIconStyle.Regular));
        Assert.IsTrue(SvgRenderer.HasVisiblePixels(SvgFile.TryParse(library.GetSvg("folder", BuiltInIconStyle.Regular)!)!));
    }

    private static VectorGlyph SquareGlyph()
    {
        return new VectorGlyph(SvgFile.TryParse(SquareGlyphSvg)!);
    }

    private static GlyphSource BuiltInSource()
    {
        var icon = new BuiltInIcon("folder", "Folder", "", [], [BuiltInIconStyle.Regular]);
        return GlyphSource.FromBuiltIn(icon, BuiltInIconStyle.Regular, SquareGlyphSvg)!;
    }

    /// <summary>A small built-in set: Folder in both styles, Folder Open in regular only, and an index entry whose SVG is missing.</summary>
    private static string CreateTestLibrary(TestFolder testFolder)
    {
        var zipPath = Path.Combine(testFolder.Root.FullName, "icons.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        void AddEntry(string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
        AddEntry("index.json", """
            {"icons":[
              {"id":"folder","name":"Folder","description":"Used to represent collections of files.","keywords":["analog","collection"],"styles":["regular","filled"]},
              {"id":"folder_open","name":"Folder Open","description":"","keywords":["files"],"styles":["regular","filled"]},
              {"id":"ghost","name":"Ghost","description":"","keywords":[],"styles":["regular"]}
            ]}
            """);
        AddEntry("icons/folder_regular.svg", Encoding.UTF8.GetString(SquareGlyphSvg));
        AddEntry("icons/folder_filled.svg", Encoding.UTF8.GetString(SquareGlyphSvg));
        AddEntry("icons/folder_open_regular.svg", Encoding.UTF8.GetString(SquareGlyphSvg));
        return zipPath;
    }

    private static (int Red, int Green, int Blue, int Alpha) PixelAt(byte[] pixels, int x, int y)
    {
        var offset = (y * Size + x) * 4;
        var alpha = pixels[offset + 3];
        return alpha == 0 ? (0, 0, 0, 0) : (pixels[offset + 2] * 255 / alpha, pixels[offset + 1] * 255 / alpha, pixels[offset] * 255 / alpha, alpha);
    }
}

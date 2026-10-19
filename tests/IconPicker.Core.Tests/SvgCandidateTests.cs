using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

/// <summary>SVG files as icon sources: found by the search, ranked with pictures, drawn and converted.</summary>
[TestClass]
public sealed class SvgCandidateTests
{
    private const string SquareLogo = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#3366cc"/></svg>""";

    [TestMethod]
    public async Task FindAsync_SquareSvg_RanksWithPicturesBelowExecutables()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        testFolder.CreateIconExe(@"Game\Game.exe");
        testFolder.CreateTextFile(@"Game\logo.svg", SquareLogo);
        testFolder.CreateFile(@"Game\logo.png", await TestImages.CreatePngAsync(64, 64));

        var candidates = await CandidateFinder.FindAsync(game, ["Game"], CancellationToken.None);

        var vector = candidates.Single(candidate => candidate.Source.Kind == IconSourceKind.Vector);
        Assert.AreEqual(IconSourceKind.Module, candidates[0].Source.Kind);
        Assert.AreEqual((CandidateFinder.VectorSize, CandidateFinder.VectorSize), (vector.Width, vector.Height));
        // A square SVG is sharp at any size, so it beats a 64 px picture of the same name.
        Assert.IsLessThan(candidates.FindIndex(candidate => candidate.Source.Kind == IconSourceKind.Image), candidates.IndexOf(vector));
    }

    [TestMethod]
    public async Task FindAsync_SvgsThatDrawNothingOrCantBeRead_AreLeftOut()
    {
        using var testFolder = new TestFolder();
        var folder = testFolder.CreateFolder("Icons");
        testFolder.CreateTextFile(@"Icons\sprites.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><defs><symbol id="a"><rect width="10" height="10"/></symbol></defs></svg>""");
        testFolder.CreateTextFile(@"Icons\template.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="context-fill"/></svg>""");
        testFolder.CreateTextFile(@"Icons\broken.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" """);
        testFolder.CreateTextFile(@"Icons\nobox.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="100%"><rect width="10" height="10"/></svg>""");
        testFolder.CreateTextFile(@"Icons\odd.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><path d="M 0 0 L x y Z" stroke-width="NaN" transform="rotate(nope)"/></svg>""");
        testFolder.CreateTextFile(@"Icons\good.svg", SquareLogo);

        var candidates = await CandidateFinder.FindAsync(folder, ["Icons"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "good.svg" }, candidates.Select(candidate => candidate.RelativePath).ToArray());
    }

    [TestMethod]
    public async Task FindAsync_LargeSvgs_AreLeftOut()
    {
        using var testFolder = new TestFolder();
        var folder = testFolder.CreateFolder("Art");
        var padding = new string(' ', (int)SvgFile.MaximumSearchedFileSize);
        testFolder.CreateTextFile(@"Art\poster.svg", SquareLogo.Replace("</svg>", padding + "</svg>", StringComparison.Ordinal));

        Assert.IsEmpty(await CandidateFinder.FindAsync(folder, ["Art"], CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_ManySvgs_OnlyTheBestAreOpened()
    {
        using var testFolder = new TestFolder();
        var app = testFolder.CreateFolder("App");
        for (var index = 0; index < CandidateFinder.InspectedVectorLimit + 20; index++)
        {
            testFolder.CreateTextFile($@"App\ui\glyph{index:000}.svg", SquareLogo);
        }
        testFolder.CreateTextFile(@"App\ui\deeper\App.svg", SquareLogo);

        var candidates = await CandidateFinder.FindAsync(app, ["App"], CancellationToken.None);

        Assert.HasCount(CandidateFinder.InspectedVectorLimit, candidates);
        // One named like the folder is always among them.
        Assert.AreEqual(@"ui\deeper\App.svg", candidates[0].RelativePath);
    }

    [TestMethod]
    public void OrderByListingDetails_ListsSvgsAfterPicturesOfTheSameNameRank()
    {
        (string Path, IconSourceKind Kind)[] files =
        [
            (@"C:\Game\ui\arrow.svg", IconSourceKind.Vector),
            (@"C:\Game\ui\arrow.png", IconSourceKind.Image),
            (@"C:\Game\Game.svg", IconSourceKind.Vector),
            (@"C:\Game\Game.exe", IconSourceKind.Module),
        ];

        var ordered = CandidateRanker.OrderByListingDetails(files, file => file.Path, file => file.Kind, file => 0, file => 0L, ["Game"]).Select(file => file.Path).ToList();

        CollectionAssert.AreEqual(new[] { @"C:\Game\Game.exe", @"C:\Game\Game.svg", @"C:\Game\ui\arrow.png", @"C:\Game\ui\arrow.svg" }, ordered);
    }

    [TestMethod]
    public async Task RenderAsync_Svg_FillsTheSquare()
    {
        using var testFolder = new TestFolder();
        var logo = testFolder.CreateTextFile("logo.svg", SquareLogo);
        var broken = testFolder.CreateTextFile("broken.svg", "<svg");

        var preview = await IconPreviewRenderer.RenderAsync(new IconSource(logo.FullName, IconSourceKind.Vector), 96);

        Assert.AreEqual((96, 96), (preview!.PixelWidth, preview.PixelHeight));
        Assert.IsNull(await IconPreviewRenderer.RenderAsync(new IconSource(broken.FullName, IconSourceKind.Vector), 96));
    }

    [TestMethod]
    public async Task ResolveAsync_Svg_IsAlwaysConvertedAndCopied()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var logo = testFolder.CreateTextFile(@"Game\logo.svg", SquareLogo);
        var empty = testFolder.CreateTextFile(@"Game\empty.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"/>""");
        var source = new IconSource(logo.FullName, IconSourceKind.Vector);

        var plan = await IconTargetResolver.ResolveAsync(game, source, IconTarget.Link, CancellationToken.None);

        Assert.IsFalse(IconTargetResolver.WillLink(game, source, IconTarget.Link));
        Assert.AreEqual(Folders.FolderIconWriter.CopiedIconSetting, plan.IconSetting);
        Assert.HasCount(7, IcoFileBuilder.ReadImageSizes(plan.IconFileBytes!)!);
        var exception = await Assert.ThrowsExactlyAsync<IconSourceException>(() => IconTargetResolver.ResolveAsync(game, new IconSource(empty.FullName, IconSourceKind.Vector), IconTarget.Copy, CancellationToken.None));
        Assert.AreEqual(IconSourceProblem.UnreadableVector, exception.Problem);
    }

    [TestMethod]
    public void GetKind_Svg_IsAConvertedVector()
    {
        Assert.AreEqual(IconSourceKind.Vector, IconSource.GetKind(@"C:\Art\Logo.SVG"));
        Assert.Contains(".svg", IconSource.SupportedExtensions);
        Assert.IsTrue(new IconSource(@"C:\Art\Logo.svg", IconSourceKind.Vector).IsConverted);
        Assert.IsFalse(new IconSource(@"C:\Art\Logo.ico", IconSourceKind.IconFile).IsConverted);
    }

    [TestMethod]
    public async Task RenderIconFileAsync_Fill_UsesTheClosestImageAndEnlarges()
    {
        var iconFile = IcoFileBuilder.Build([IcoImage.FromPng(32, await TestImages.CreatePngAsync(32, 32)), IcoImage.FromPng(256, await TestImages.CreatePngAsync(256, 256, red: 0, green: 255))]);
        var smallOnly = IcoFileBuilder.Build([IcoImage.FromPng(32, await TestImages.CreatePngAsync(32, 32))]);

        var at24 = await IconPreviewRenderer.RenderIconFileAsync(iconFile, 24, IconFit.Fill);
        var at154 = await IconPreviewRenderer.RenderIconFileAsync(iconFile, 154, IconFit.Fill);
        var enlarged = await IconPreviewRenderer.RenderIconFileAsync(smallOnly, 154, IconFit.Fill);
        var shrinkOnly = await IconPreviewRenderer.RenderIconFileAsync(smallOnly, 154);

        Assert.AreEqual((24, 24), (at24!.PixelWidth, at24.PixelHeight));
        Assert.AreEqual((154, 154), (at154!.PixelWidth, at154.PixelHeight));
        Assert.AreEqual((154, 154), (enlarged!.PixelWidth, enlarged.PixelHeight));
        Assert.AreEqual((32, 32), (shrinkOnly!.PixelWidth, shrinkOnly.PixelHeight));
        // The 32 px image is red, the 256 px one green: each size comes from the closest image of at least that size.
        var centerAt24 = IconPreviewRenderer.ToSquarePixels(at24, 24).AsSpan(((12 * 24) + 12) * 4, 4).ToArray();
        var centerAt154 = IconPreviewRenderer.ToSquarePixels(at154, 154).AsSpan(((77 * 154) + 77) * 4, 4).ToArray();
        CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, centerAt24);
        CollectionAssert.AreEqual(new byte[] { 0, 255, 0, 255 }, centerAt154);
    }

    [TestMethod]
    public async Task ResolveAsync_BuiltIcon_IsCopiedEvenWhenItLiesInsideTheFolder()
    {
        using var testFolder = new TestFolder();
        var profile = testFolder.CreateFolder("Profile");
        var builtIcon = testFolder.CreateFile(@"Profile\AppData\Icon Picker\Built\icon.ico", IcoFileBuilder.Build([IcoImage.FromPng(256, await TestImages.CreatePngAsync(256, 256))]));
        var source = new IconSource(builtIcon.FullName, IconSourceKind.IconFile, IsAppData: true);

        var plan = await IconTargetResolver.ResolveAsync(profile, source, IconTarget.Link, CancellationToken.None);

        Assert.IsFalse(IconTargetResolver.WillLink(profile, source, IconTarget.Link));
        Assert.IsTrue(IconTargetResolver.WillLink(profile, source with { IsAppData = false }, IconTarget.Link));
        Assert.AreEqual(Folders.FolderIconWriter.CopiedIconSetting, plan.IconSetting);
    }
}

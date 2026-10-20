using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class CandidateTests
{
    [TestMethod]
    [DataRow(@"C:\Games\Hades\Hades.exe", 0)]
    [DataRow(@"C:\Games\Hades\hades_launcher.exe", 1)]
    [DataRow(@"C:\Games\Hades\icon.png", 1)]
    [DataRow(@"C:\Games\Hades\GameLogo.png", 1)]
    [DataRow(@"C:\Games\Hades\Engine.exe", 2)]
    [DataRow(@"C:\Games\Hades\hd.exe", 2)]
    public void GetNameRank_ComparesNormalizedNames(string filePath, int expectedRank)
    {
        Assert.AreEqual(expectedRank, CandidateRanker.GetNameRank(filePath, ["Hades"]));
    }

    [TestMethod]
    public void GetNameRank_AnyOfTheFolderNamesCounts()
    {
        Assert.AreEqual(0, CandidateRanker.GetNameRank(@"C:\Tools\Release\Tools.exe", ["Release", "Tools"]));
    }

    // Cases from a survey of real game and app folders: a trailing size doesn't count against an .ico or picture name, while digits that are only partly a size, and executable names, are compared as they are.
    [TestMethod]
    [DataRow(@"C:\Games\Steam\steamapps\common\tModLoader\Libraries\Native\tModLoader.bmp", "tModLoader", 0)]
    [DataRow(@"C:\Games\Steam\steamapps\common\tModLoader\Libraries\Native\tModLoader256.bmp", "tModLoader", 0)]
    [DataRow(@"C:\Games\Steam\steamapps\common\tModLoader\Libraries\Native\tModLoader16.bmp", "tModLoader", 0)]
    [DataRow(@"D:\Apps\Hydrus Network - AI Art\static\hydrus_128.ico", "Hydrus Network - AI Art", 1)]
    [DataRow(@"C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe", "AutoHotkey", 1)]
    [DataRow(@"C:\Games\DOOM\DOOM 64\DOOM64.exe", "DOOM", 1)]
    [DataRow(@"C:\Games\DOOM 64\DOOM64.exe", "DOOM 64", 0)]
    [DataRow(@"C:\Games\Steam\steamapps\common\Expedition 33\Expedition33_256.png", "Expedition 33", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste@2x.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste@1.5x.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste_256x256.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste 150px.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\CelesteHD.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste_large.png", "Celeste", 0)]
    [DataRow(@"C:\Games\Celeste\Celeste-1.16.png", "Celeste", 1)]
    [DataRow(@"C:\Games\Celeste\Celeste_016.png", "Celeste", 1)]
    [DataRow(@"C:\Games\Celeste\Celeste2016.png", "Celeste", 1)]
    [DataRow(@"C:\Games\Celeste\Celeste_1280x1024.jpg", "Celeste", 1)]
    [DataRow(@"C:\Program Files\DLSS Swapper\Assets\icon_256.png", "DLSS Swapper", 1)]
    [DataRow(@"C:\Users\Me\AppData\Local\Programs\anytype\resources\icons\256x256.png", "anytype", 2)]
    [DataRow(@"D:\Apps\MusicBee\Skins\Dark\Bee78.jpg", "MusicBee", 2)]
    public void GetNameRank_SizeMarkers(string filePath, string folderName, int expectedRank)
    {
        Assert.AreEqual(expectedRank, CandidateRanker.GetNameRank(filePath, [folderName]));
    }

    [TestMethod]
    [DataRow("tModLoader256", "tModLoader")]
    [DataRow("icon200", "icon")]
    [DataRow("icon_512x512@2x", "icon")]
    [DataRow("StoreLogo.scale-200", "StoreLogo")]
    [DataRow("150x150Logo.scale-100_contrast-white", "150x150Logo")]
    [DataRow("vlc16x16", "vlc")]
    [DataRow("icon_48", "icon")]
    [DataRow("Game 150px", "Game")]
    [DataRow("typora-file-icon@2x", "typora-file-icon")]
    [DataRow("CursorHi@1.25x", "CursorHi")]
    [DataRow("WinMergeLogoSmall", "WinMergeLogo")]
    [DataRow("Game-HD", "Game")]
    [DataRow("Item.Food.Dish.016", "Item.Food.Dish.016")]
    [DataRow("LY016", "LY016")]
    [DataRow("Celeste2016", "Celeste2016")]
    [DataRow("Game-1.16", "Game-1.16")]
    [DataRow("stellaris_1280x1024", "stellaris_1280x1024")]
    [DataRow("export128x64", "export128x64")]
    [DataRow("PotPlayerMini", "PotPlayerMini")]
    public void GetNameWithoutSizeMarker_RemovesOnlyAWholeTrailingSize(string fileName, string expectedName)
    {
        Assert.AreEqual(expectedName, CandidateRanker.GetNameWithoutSizeMarker(fileName));
    }

    [TestMethod]
    public void Rank_WidePictureWithASizeNumber_StaysBelowASquareIcon()
    {
        var wallpaper = new IconCandidate(new IconSource(@"C:\Games\Celeste\Celeste_1024.jpg", IconSourceKind.Image), "Celeste_1024.jpg", 0, 300_000, 1024, 576);
        var icon = new IconCandidate(new IconSource(@"C:\Games\Celeste\celeste_icon.png", IconSourceKind.Image), "celeste_icon.png", 0, 40_000, 256, 256);

        var ranked = CandidateRanker.Rank([wallpaper, icon], ["Celeste"]);

        Assert.AreSame(icon, ranked[0]);
    }

    [TestMethod]
    public async Task FindAsync_SizeVariantsOfOneIcon_LargestFirst()
    {
        // The layout reported in tModLoader: the unsuffixed copy is 32 px, the middle size.
        using var testFolder = new TestFolder();
        var tModLoader = testFolder.CreateFolder("tModLoader");
        foreach (var (fileName, size) in new[] { ("tModLoader.bmp", 32), ("tModLoader16.bmp", 16), ("tModLoader24.bmp", 24), ("tModLoader48.bmp", 48), ("tModLoader256.bmp", 256) })
        {
            testFolder.CreateFile(Path.Combine("tModLoader", "Libraries", "Native", fileName), await TestImages.CreateBmpAsync(size, size));
        }

        var candidates = await CandidateFinder.FindAsync(tModLoader, ["tModLoader"], CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "tModLoader256.bmp", "tModLoader48.bmp", "tModLoader.bmp", "tModLoader24.bmp", "tModLoader16.bmp" },
            candidates.Select(candidate => Path.GetFileName(candidate.RelativePath)).ToArray());
    }

    [TestMethod]
    public void Rank_FollowsTheDocumentedOrder()
    {
        IconCandidate Candidate(string relativePath, IconSourceKind kind, int depth, long fileSize, int width, int height, int iconIndex = 0)
        {
            return new IconCandidate(new IconSource(Path.Combine(@"C:\Games\Hades", relativePath), kind, iconIndex), relativePath, depth, fileSize, width, height);
        }

        var candidates = new[]
        {
            Candidate("cover.png", IconSourceKind.Image, 0, 500_000, 600, 300),
            Candidate("square.png", IconSourceKind.Image, 0, 50_000, 128, 128),
            Candidate("big.png", IconSourceKind.Image, 0, 90_000, 512, 512),
            Candidate(@"Tools\Editor.exe", IconSourceKind.Module, 1, 9_000_000, 256, 256),
            Candidate("Engine.exe", IconSourceKind.Module, 0, 1_000, 256, 256),
            Candidate("Engine2.exe", IconSourceKind.Module, 0, 2_000, 256, 256),
            Candidate("logo.ico", IconSourceKind.IconFile, 0, 10_000, 256, 256),
            Candidate("Hades.exe", IconSourceKind.Module, 0, 1_000, 256, 256, iconIndex: 1),
            Candidate("Hades.exe", IconSourceKind.Module, 0, 1_000, 256, 256),
        };

        var ranked = CandidateRanker.Rank(candidates, ["Hades"]);

        CollectionAssert.AreEqual(
            new[] { "Hades.exe#0", "logo.ico#0", "Engine2.exe#0", "Engine.exe#0", @"Tools\Editor.exe#0", "Hades.exe#1", "big.png#0", "square.png#0", "cover.png#0" },
            ranked.Select(candidate => $"{candidate.RelativePath}#{candidate.Source.IconIndex}").ToArray());
    }

    [TestMethod]
    public void Rank_LowResolutionIcons_RankWithPictures()
    {
        var smallExe = Candidate(@"Game.exe", IconSourceKind.Module, 0, 5_000_000, 32, 32);
        var mediumExe = Candidate(@"Game.exe", IconSourceKind.Module, 0, 5_000_000, 48, 48);
        var largeIcon = Candidate(@"Game.ico", IconSourceKind.IconFile, 0, 100_000, 256, 256);
        var largePicture = Candidate(@"Game_256.png", IconSourceKind.Image, 0, 60_000, 256, 256);
        var widePicture = Candidate(@"cover.png", IconSourceKind.Image, 0, 300_000, 600, 300);

        Assert.AreSame(largePicture, CandidateRanker.Rank([smallExe, largePicture], ["Game"])[0]);
        Assert.AreSame(smallExe, CandidateRanker.Rank([smallExe, widePicture], ["Game"])[0]);
        Assert.AreSame(largeIcon, CandidateRanker.Rank([mediumExe, largeIcon], ["Game"])[0]);
    }

    [TestMethod]
    public void Rank_BlurryIcon_LosesToASharpEnoughPicture()
    {
        var blurryExe = Candidate(@"Game.exe", IconSourceKind.Module, 0, 5_000_000, 47, 47);
        var blurryIcon = Candidate(@"Game.ico", IconSourceKind.IconFile, 0, 5_000, 32, 32);
        var mediumPicture = Candidate(@"Game.png", IconSourceKind.Image, 0, 30_000, 128, 128);
        var smallPicture = Candidate(@"Game64.png", IconSourceKind.Image, 0, 9_000, 64, 64);
        var sharpEnoughExe = Candidate(@"Game.exe", IconSourceKind.Module, 0, 5_000_000, 48, 48);
        var largePicture = Candidate(@"Game_256.png", IconSourceKind.Image, 0, 60_000, 256, 256);

        Assert.AreSame(mediumPicture, CandidateRanker.Rank([blurryExe, mediumPicture], ["Game"])[0]);
        Assert.AreSame(mediumPicture, CandidateRanker.Rank([blurryIcon, mediumPicture], ["Game"])[0]);
        Assert.AreSame(smallPicture, CandidateRanker.Rank([blurryExe, smallPicture], ["Game"])[0]);
        // 48 px is the boundary: from there an executable stays above every picture.
        Assert.AreSame(sharpEnoughExe, CandidateRanker.Rank([largePicture, sharpEnoughExe], ["Game"])[0]);
    }

    [TestMethod]
    public void Rank_SharpSecondaryIcon_NeverBeatsItsOwnBlurryMainIcon()
    {
        // Project Starborn: the Unreal bootstrap exe's main icon is the 32 px game icon, its secondary icon the 256 px engine logo.
        var mainIcon = Candidate(@"Untitled_Space_Game.exe", IconSourceKind.Module, 0, 174_080, 32, 32);
        var engineLogo = Candidate(@"Untitled_Space_Game.exe", IconSourceKind.Module, 0, 174_080, 256, 256, iconIndex: 1);
        var shippingExe = Candidate(@"Untitled_Space_Game\Binaries\Win64\Untitled_Space_Game-Win64-Shipping.exe", IconSourceKind.Module, 3, 196_000_000, 32, 32);

        var ranked = CandidateRanker.Rank([engineLogo, shippingExe, mainIcon], ["Project Starborn"]);

        CollectionAssert.AreEqual(new[] { mainIcon, shippingExe, engineLogo }, ranked.ToArray());
    }

    [TestMethod]
    [DataRow("Sandboxie-Plus", 1)]
    [DataRow("SANDBOXIE PLUS", 1)]
    [DataRow("Sandboxie", 2)]
    [DataRow("Xanasoft Crash Dump Utility", 2)]
    [DataRow(null, 2)]
    public void GetNameRank_ProductNameCountsOnlyWhenItIsTheFolderName(string? productName, int expectedRank)
    {
        Assert.AreEqual(expectedRank, CandidateRanker.GetNameRank(@"C:\Program Files\Sandboxie-Plus\SandMan.exe", ["Sandboxie-Plus"], productName: productName));
    }

    [TestMethod]
    public void Rank_MainAppByProductName_BeatsSharperHelpers()
    {
        // Sandboxie-Plus: SandMan.exe (96 px) is the app; Start.exe and MiniDump.exe are helpers with 256 px icons.
        var start = Candidate(@"Start.exe", IconSourceKind.Module, 0, 368_552, 256, 256) with { ProductName = "Sandboxie" };
        var miniDump = Candidate(@"MiniDump.exe", IconSourceKind.Module, 0, 111_016, 256, 256) with { ProductName = "Xanasoft Crash Dump Utility" };
        var sandMan = Candidate(@"SandMan.exe", IconSourceKind.Module, 0, 4_498_856, 96, 96) with { ProductName = "Sandboxie-Plus" };

        Assert.AreSame(sandMan, CandidateRanker.Rank([start, miniDump, sandMan], ["Sandboxie-Plus"])[0]);
    }

    [TestMethod]
    public async Task FindAsync_ReadsTheExecutableProductName()
    {
        using var testFolder = new TestFolder();
        var folder = testFolder.CreateFolder("Tools");
        testFolder.CreateIconExe(@"Tools\host.exe");

        var candidates = await CandidateFinder.FindAsync(folder, ["Tools"], CancellationToken.None);

        Assert.AreEqual(System.Diagnostics.FileVersionInfo.GetVersionInfo(TestFolder.IconExePath).ProductName, candidates[0].ProductName);
    }

    [TestMethod]
    public void Rank_FirstReadableIcon_IsTheMainOne()
    {
        // Icon 0 was a placeholder and left out, so icon 1 is the executable's main icon, not a secondary one.
        var gameIcon = Candidate(@"Game.exe", IconSourceKind.Module, 0, 3_000_000, 256, 256, iconIndex: 1);
        var editor = Candidate(@"Tools\Editor.exe", IconSourceKind.Module, 1, 9_000_000, 256, 256);

        Assert.AreSame(gameIcon, CandidateRanker.Rank([editor, gameIcon], ["Game"])[0]);
    }

    [TestMethod]
    public void Rank_BestCopyNamedLikeTheFolder_WinsWhateverItsDepth()
    {
        // Bandizip ships a 48 px shellicons\bandizip.ico and a 256 px icons\default\Bandizip.ico.
        var shellIcon = Candidate(@"shellicons\bandizip.ico", IconSourceKind.IconFile, 1, 20_000, 48, 48);
        var defaultIcon = Candidate(@"icons\default\Bandizip.ico", IconSourceKind.IconFile, 2, 90_000, 256, 256);
        // Generic names keep "closer first": each mod of a game can ship its own icon.png.
        var gameIcon = Candidate(@"icon.png", IconSourceKind.Image, 0, 9_000, 128, 128);
        var modIcon = Candidate(@"Mods\Some Mod\icon.png", IconSourceKind.Image, 2, 50_000, 256, 256);

        Assert.AreSame(defaultIcon, CandidateRanker.Rank([shellIcon, defaultIcon], ["Bandizip"])[0]);
        Assert.AreSame(gameIcon, CandidateRanker.Rank([modIcon, gameIcon], ["Game"])[0]);
    }

    [TestMethod]
    public void Rank_EquallySharpExecutables_CloserFirst()
    {
        // Grim Dawn ships the same exe at the root, in compat\ and in x64\; the deeper copies are larger files.
        var rootExe = Candidate(@"Grim Dawn.exe", IconSourceKind.Module, 0, 3_000_000, 256, 256);
        var compatExe = Candidate(@"compat\Grim Dawn.exe", IconSourceKind.Module, 1, 9_000_000, 256, 256);

        Assert.AreSame(rootExe, CandidateRanker.Rank([compatExe, rootExe], ["Grim Dawn"])[0]);
    }

    [TestMethod]
    public void Rank_ExecutableNamedLikeTheFolder_BeatsTheDllNamedLikeIt()
    {
        // Vivaldi's vivaldi.dll sits in a version folder and is far larger than vivaldi.exe.
        var exe = Candidate(@"Application\vivaldi.exe", IconSourceKind.Module, 1, 3_000_000, 256, 256);
        var dll = Candidate(@"Application\7.9.3970.60\vivaldi.dll", IconSourceKind.Module, 2, 300_000_000, 256, 256);
        var sameDepthDll = Candidate(@"Application\vivaldi.dll", IconSourceKind.Module, 1, 300_000_000, 256, 256);

        Assert.AreSame(exe, CandidateRanker.Rank([dll, exe], ["Vivaldi"])[0]);
        Assert.AreSame(exe, CandidateRanker.Rank([sameDepthDll, exe], ["Vivaldi"])[0]);
    }

    [TestMethod]
    public void IsNearlySquare_AllowsFivePercent()
    {
        Assert.IsTrue(Candidate("a.png", IconSourceKind.Image, 0, 1, 100, 95).IsNearlySquare);
        Assert.IsFalse(Candidate("a.png", IconSourceKind.Image, 0, 1, 100, 94).IsNearlySquare);
    }

    [TestMethod]
    public void OrderByListingDetails_KeepsFilesNamedLikeTheFolderFirst()
    {
        // Before files are opened their resolution is unknown, so a deep picture or .dll named like the folder must not fall behind hundreds of unrelated icons.
        var files = new (string Path, IconSourceKind Kind, int Depth, long Size)[]
        {
            (@"C:\Pack\Toolbar\tool1.ico", IconSourceKind.IconFile, 1, 900),
            (@"C:\Pack\Toolbar\tool2.exe", IconSourceKind.Module, 1, 90_000),
            (@"C:\Pack\Art\Icons\Pack.png", IconSourceKind.Image, 2, 40_000),
            (@"C:\Pack\Bin\Pack.dll", IconSourceKind.Module, 1, 9_000_000),
        };

        var ordered = CandidateRanker.OrderByListingDetails(files, file => file.Path, file => file.Kind, file => file.Depth, file => file.Size, ["Pack"]).ToList();

        CollectionAssert.AreEqual(new[] { @"C:\Pack\Bin\Pack.dll", @"C:\Pack\Art\Icons\Pack.png" }, ordered.Take(2).Select(file => file.Path).ToArray());
    }

    [TestMethod]
    public void Rank_NearlySquarePicture_CountsAsSquare()
    {
        var nearlySquare = Candidate(@"Game.png", IconSourceKind.Image, 0, 200_000, 512, 511);
        var tiny = Candidate(@"Game16.png", IconSourceKind.Image, 0, 1_000, 16, 16);

        Assert.AreSame(nearlySquare, CandidateRanker.Rank([tiny, nearlySquare], ["Game"])[0]);
    }

    [TestMethod]
    public void Rank_SecondaryIcons_ComeAfterOtherMainIcons()
    {
        var mainIcon = Candidate(@"Hades.exe", IconSourceKind.Module, 0, 1_000, 256, 256);
        var documentIcon = Candidate(@"Hades.exe", IconSourceKind.Module, 0, 1_000, 256, 256, iconIndex: 1);
        var logo = Candidate(@"logo.ico", IconSourceKind.IconFile, 0, 10_000, 256, 256);
        var otherExe = Candidate(@"Tools\Editor.exe", IconSourceKind.Module, 1, 9_000, 256, 256);
        var picture = Candidate(@"Hades.png", IconSourceKind.Image, 0, 90_000, 512, 512);

        var ranked = CandidateRanker.Rank([documentIcon, otherExe, picture, logo, mainIcon], ["Hades"]);

        CollectionAssert.AreEqual(new[] { mainIcon, logo, otherExe, documentIcon, picture }, ranked.ToArray());
    }

    [TestMethod]
    [DataRow(@"C:\Games\Hades\app_icon.png", 1)]
    [DataRow(@"C:\Games\Hades\AppIcon.png", 1)]
    [DataRow(@"C:\Games\Hades\APPICON.ICO", 1)]
    [DataRow(@"C:\Games\Hades\Game_Icon@2x.png", 1)]
    [DataRow(@"C:\Games\Hades\HTMLIcon.png", 1)]
    [DataRow(@"C:\Games\Hades\lexicon.png", 2)]
    [DataRow(@"C:\Games\Hades\iconbg.png", 2)]
    [DataRow(@"C:\Games\Hades\IconSet.png", 2)]
    [DataRow(@"C:\Games\Hades\BuffIcon_01.png", 2)]
    [DataRow(@"C:\Games\Hades\ItemIcons.png", 2)]
    [DataRow(@"C:\Games\Hades\logo_white.png", 2)]
    [DataRow(@"C:\Games\Hades\logout.png", 2)]
    [DataRow(@"C:\Games\Hades\favicon.ico", 1)]
    [DataRow(@"C:\Program Files\Typora\resources\assets\icon\icon_512x512@2x.png", 1)]
    [DataRow(@"C:\Program Files\DLSS Swapper\Assets\StoreLogo.scale-200.png", 1)]
    [DataRow(@"C:\Program Files\DLSS Swapper\Assets\Square44x44Logo.targetsize-256.png", 1)]
    [DataRow(@"C:\Program Files\DLSS Swapper\Assets\Square44x44Logo.altform-unplated_targetsize-256.png", 1)]
    [DataRow(@"C:\Games\Hades\icon游戏.png", 2)]
    [DataRow(@"C:\Games\Hades\游戏icon.png", 1)]
    [DataRow(@"C:\Games\StarsandIsland\WebAssets\logo_英文.webp", 2)]
    public void GetNameRank_GenericIconWordOnlyAsTheLastWord(string filePath, int expectedRank)
    {
        Assert.AreEqual(expectedRank, CandidateRanker.GetNameRank(filePath, ["Hades"]));
    }

    [TestMethod]
    public async Task FindAsync_DllNamedLikeTheFolder_IsSearched()
    {
        // Apps started through dotnet keep their icon in their main .dll (tModLoader.dll); other libraries stay out.
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        testFolder.CreateIconExe(@"Game\Game.dll");
        testFolder.CreateIconExe(@"Game\Other.dll");
        testFolder.CreateIconExe(@"Game\Game64.dll");
        // Names under 3 characters never count as named like the folder.
        var qt = testFolder.CreateFolder("Qt");
        testFolder.CreateIconExe(@"Qt\Qt.dll");

        var gameCandidates = await CandidateFinder.FindAsync(game, ["Game"], CancellationToken.None);
        var qtCandidates = await CandidateFinder.FindAsync(qt, ["Qt"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Game.dll" }, gameCandidates.Select(candidate => candidate.RelativePath).Distinct().ToArray());
        Assert.IsEmpty(qtCandidates);
    }

    private static IconCandidate Candidate(string relativePath, IconSourceKind kind, int depth, long fileSize, int width, int height, int iconIndex = 0)
    {
        return new IconCandidate(new IconSource(Path.Combine(@"C:\Games\Folder", relativePath), kind, iconIndex), relativePath, depth, fileSize, width, height);
    }

    [TestMethod]
    public async Task FindAsync_FindsAndRanksTheFolderCandidates()
    {
        using var testFolder = new TestFolder();
        var hades = testFolder.CreateFolder("Hades");
        testFolder.CreateIconExe(@"Hades\Hades.exe");
        testFolder.CreateIconExe(@"Hades\unins000.exe");
        testFolder.CreateIconExe(@"Hades\Tools\Editor.exe");
        testFolder.CreateFile(@"Hades\cover.png", await TestImages.CreatePngAsync(300, 150));
        testFolder.CreateFile(@"Hades\icon.png", await TestImages.CreatePngAsync(256, 256));
        testFolder.CreateFile(@"Hades\tiny.png", await TestImages.CreatePngAsync(8, 8));
        testFolder.CreateFile(@"Hades\logo.ico", ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!);
        testFolder.CreateFile(@"Hades\node_modules\skipped.png", await TestImages.CreatePngAsync(64, 64));
        testFolder.CreateTextFile(@"Hades\readme.txt", "not an icon");
        FolderIconWriter.SetIcon(hades, IconWritePlan.Copy(ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!));

        var candidates = await CandidateFinder.FindAsync(hades, ["Hades"], CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Hades.exe", "logo.ico", @"Tools\Editor.exe", "icon.png", "cover.png" },
            candidates.Select(candidate => candidate.RelativePath).Distinct().ToArray());
        var cover = candidates.Single(candidate => candidate.RelativePath == "cover.png");
        Assert.AreEqual((300, 150), (cover.Width, cover.Height));
        Assert.AreEqual(IconSourceKind.Image, cover.Source.Kind);
    }

    [TestMethod]
    public async Task InspectFileAsync_FileOutsideTheFolder_KeepsItsFullPath()
    {
        using var testFolder = new TestFolder();
        var hades = testFolder.CreateFolder("Hades");
        var icon = testFolder.CreateFile(@"Icons\hades.ico", ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!);

        var candidates = await CandidateFinder.InspectFileAsync(icon.FullName, hades);

        Assert.HasCount(1, candidates);
        Assert.AreEqual(icon.FullName, candidates[0].RelativePath);
    }

    [TestMethod]
    public async Task InspectFileAsync_Shortcut_OffersTheIconItShows()
    {
        using var testFolder = new TestFolder();
        var hades = testFolder.CreateFolder("Hades");
        var exe = testFolder.CreateIconExe(@"Elsewhere\Game.exe");
        var shortcut = testFolder.CreateShortcut(@"Desktop\Game.lnk", exe.FullName);

        var candidates = await CandidateFinder.InspectFileAsync(shortcut.FullName, hades);

        Assert.HasCount(1, candidates);
        Assert.AreEqual(exe.FullName, candidates[0].Source.FilePath);
        Assert.AreEqual(shortcut.FullName, candidates[0].ShortcutPath);
    }
}

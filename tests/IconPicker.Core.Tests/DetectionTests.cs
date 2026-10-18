using IconPicker.Core.Detection;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class DetectionTests
{
    [TestMethod]
    public void GetCandidateExes_TakesBinFoldersAndSkipsIgnoredExes()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        testFolder.CreateIconExe(@"Game\Game.exe");
        testFolder.CreateIconExe(@"Game\unins000.exe");
        testFolder.CreateIconExe(@"Game\bin\x64\GameServer.exe");
        testFolder.CreateIconExe(@"Game\Tools\Editor.exe");

        var candidates = AppFolderDetector.GetCandidateExes(game);

        CollectionAssert.AreEquivalent(new[] { ("Game.exe", 0), ("GameServer.exe", 2) }, candidates.Select(candidate => (candidate.File.Name, candidate.Depth)).ToArray());
    }

    [TestMethod]
    public void IsAppFolder_NeedsAnExeWithAnIcon()
    {
        using var testFolder = new TestFolder();
        // Not named "Game": a subfolder called Game is bin-style, so its .exe would count for the folder above it.
        testFolder.CreateIconExe(@"Hades\Hades.exe");
        testFolder.CreateTextFile(@"Broken\Broken.exe", "not an executable");

        Assert.IsTrue(AppFolderDetector.IsAppFolder(new DirectoryInfo(Path.Combine(testFolder.Root.FullName, "Hades"))));
        Assert.IsFalse(AppFolderDetector.IsAppFolder(new DirectoryInfo(Path.Combine(testFolder.Root.FullName, "Broken"))));
        Assert.IsFalse(AppFolderDetector.IsAppFolder(testFolder.Root));
    }

    [TestMethod]
    public void FindAppFolders_FollowsTheScriptRules()
    {
        using var testFolder = new TestFolder();
        CreateLibrary(testFolder);

        var appFolders = AppFolderDetector.FindAppFolders(testFolder.Root, CancellationToken.None);

        var found = appFolders.Select(appFolder => (Path.GetRelativePath(testFolder.Root.FullName, appFolder.Folder.FullName), appFolder.OuterFolder?.Name)).ToArray();
        CollectionAssert.AreEquivalent(
            new (string, string?)[]
            {
                ("GameA", null),
                (@"Steam", null),
                (@"Steam\steamapps\common\GameE", null),
                (@"Tools\Release", "Tools"),
            },
            found);
    }

    [TestMethod]
    public void GetDirectRows_TakesEachSubfolderWorthSearching()
    {
        using var testFolder = new TestFolder();
        CreateLibrary(testFolder);

        var rows = BatchRowSource.GetDirectRows(testFolder.Root);

        CollectionAssert.AreEqual(new[] { "GameA", "Steam", "Tools" }, rows.Select(row => row.Folder.Name).ToArray());
    }

    [TestMethod]
    public void GetDeepRows_AddsOuterFoldersAboveTheirApp()
    {
        using var testFolder = new TestFolder();
        CreateLibrary(testFolder);

        var rows = BatchRowSource.GetDeepRows(testFolder.Root, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "GameA", "Steam", @"Steam\steamapps\common\GameE", "Tools", @"Tools\Release" },
            rows.Select(row => Path.GetRelativePath(testFolder.Root.FullName, row.Folder.FullName)).ToArray());
        CollectionAssert.AreEqual(new[] { "Tools", "Release" }, rows[3].SearchNames.ToArray());
        CollectionAssert.AreEqual(new[] { "Release", "Tools" }, rows[4].SearchNames.ToArray());
    }

    [TestMethod]
    public void GetDirectRows_ListsRepositoriesLikeAnyFolder()
    {
        // A downloaded tool is often a git clone, such as IdleOn Injector with InjectCheatsUI.exe at its top.
        using var testFolder = new TestFolder();
        testFolder.CreateIconExe(@"IdleOn Injector\InjectCheatsUI.exe");
        testFolder.CreateFolder(@"IdleOn Injector\.git");

        var rows = BatchRowSource.GetDirectRows(testFolder.Root);

        CollectionAssert.AreEqual(new[] { "IdleOn Injector" }, rows.Select(row => row.Folder.Name).ToArray());
    }

    [TestMethod]
    public void GetDeepRows_RepositoryCountsOnlyWithAnExeAtItsTop()
    {
        using var testFolder = new TestFolder();
        // A second app at the top, so the library itself doesn't become the outer folder of the only app.
        testFolder.CreateIconExe(@"GameA\GameA.exe");
        testFolder.CreateIconExe(@"Tools\IdleOn Injector\InjectCheatsUI.exe");
        testFolder.CreateFolder(@"Tools\IdleOn Injector\.git");
        // A code project: its .exe is a build output deep inside.
        testFolder.CreateIconExe(@"Tools\Project\build\Debug\Project.exe");
        testFolder.CreateFolder(@"Tools\Project\.git");

        var rows = BatchRowSource.GetDeepRows(testFolder.Root, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "GameA", "Tools", @"Tools\IdleOn Injector" },
            rows.Select(row => Path.GetRelativePath(testFolder.Root.FullName, row.Folder.FullName)).ToArray());
    }

    [TestMethod]
    public void GetDirectRows_SkipsHiddenFolders()
    {
        // Google Drive keeps hidden .tmp.drivedownload and .tmp.driveupload folders at its top.
        using var testFolder = new TestFolder();
        testFolder.CreateIconExe(@"GameA\GameA.exe");
        testFolder.CreateFolder(".tmp.drivedownload").Attributes |= FileAttributes.Hidden;

        var rows = BatchRowSource.GetDirectRows(testFolder.Root);

        CollectionAssert.AreEqual(new[] { "GameA" }, rows.Select(row => row.Folder.Name).ToArray());
    }

    [TestMethod]
    public void GetDeepRows_WalksThroughHiddenFoldersWithoutListingThem()
    {
        // Apps can sit under a hidden folder (AppData\Local\Programs); a hidden app folder itself gets no row.
        using var testFolder = new TestFolder();
        testFolder.CreateFolder("AppData").Attributes |= FileAttributes.Hidden;
        testFolder.CreateIconExe(@"AppData\Local\Programs\Tool\Tool.exe");
        testFolder.CreateIconExe(@"AppData\Local\Programs\Other\Other.exe");
        testFolder.CreateIconExe(@".cache\Helper.exe");
        new DirectoryInfo(Path.Combine(testFolder.Root.FullName, ".cache")).Attributes |= FileAttributes.Hidden;

        var rows = BatchRowSource.GetDeepRows(testFolder.Root, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { @"AppData\Local\Programs\Other", @"AppData\Local\Programs\Tool" },
            rows.Select(row => Path.GetRelativePath(testFolder.Root.FullName, row.Folder.FullName)).ToArray());
    }

    [TestMethod]
    public void Detect_ChoosesTheModeFromTheFolder()
    {
        using var testFolder = new TestFolder();
        CreateLibrary(testFolder);

        Assert.AreEqual(new ModeDetection(FolderMode.Single, false), ModeDetector.Detect(new DirectoryInfo(Path.Combine(testFolder.Root.FullName, "GameA"))));
        Assert.AreEqual(new ModeDetection(FolderMode.Batch, false), ModeDetector.Detect(testFolder.Root));
        Assert.AreEqual(new ModeDetection(FolderMode.Batch, true), ModeDetector.Detect(new DirectoryInfo(Path.GetPathRoot(testFolder.Root.FullName)!)));
    }

    private static void CreateLibrary(TestFolder testFolder)
    {
        testFolder.CreateIconExe(@"GameA\GameA.exe");
        testFolder.CreateIconExe(@"Tools\Release\tool.exe");
        testFolder.CreateIconExe(@"Steam\steam.exe");
        testFolder.CreateIconExe(@"Steam\steamapps\common\GameE\GameE.exe");
        testFolder.CreateIconExe(@"node_modules\package\cli.exe");
    }
}

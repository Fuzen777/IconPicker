using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class ShortcutTests
{
    [TestMethod]
    public void ReadIcon_LinkWithItsOwnIcon_GivesThatIcon()
    {
        using var testFolder = new TestFolder();
        var icon = testFolder.CreateFile(@"Icons\game.ico", ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!);
        var shortcut = testFolder.CreateShortcut("Game.lnk", TestFolder.IconExePath, $"{icon.FullName},0");

        Assert.AreEqual(new ShortcutIcon(icon.FullName, 0), ShortcutReader.ReadIcon(shortcut.FullName));
    }

    [TestMethod]
    public void ReadIcon_LinkToAnExecutable_GivesItsFirstIcon()
    {
        using var testFolder = new TestFolder();
        var exe = testFolder.CreateIconExe(@"Apps\Game.exe");
        var shortcut = testFolder.CreateShortcut("Game.lnk", exe.FullName);

        Assert.AreEqual(new ShortcutIcon(exe.FullName, 0), ShortcutReader.ReadIcon(shortcut.FullName));
    }

    [TestMethod]
    public void ReadIcon_LinkToADocument_GivesNothing()
    {
        // Its icon is the file type's, which says nothing about the app.
        using var testFolder = new TestFolder();
        var document = testFolder.CreateTextFile("readme.txt", "text");
        var shortcut = testFolder.CreateShortcut("Readme.lnk", document.FullName);

        Assert.IsNull(ShortcutReader.ReadIcon(shortcut.FullName));
    }

    [TestMethod]
    public void ReadIcon_InternetShortcut_GivesItsIconFile()
    {
        // As Steam writes them: the icon is the game's .ico in Steam's folder.
        using var testFolder = new TestFolder();
        var icon = testFolder.CreateFile(@"Steam\steam\games\5f3a.ico", ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!);
        var shortcut = testFolder.CreateTextFile("Hades.url", $"[{{000214A0-0000-0000-C000-000000000046}}]\r\nProp3=19,0\r\n[InternetShortcut]\r\nIDList=\r\nIconIndex=2\r\nURL=steam://rungameid/1145360\r\nIconFile={icon.FullName}\r\n");

        Assert.AreEqual(new ShortcutIcon(icon.FullName, 2), ShortcutReader.ReadIcon(shortcut.FullName));
    }

    [TestMethod]
    public void ReadIcon_InternetShortcutWithoutALocalIcon_GivesNothing()
    {
        using var testFolder = new TestFolder();
        var webIcon = testFolder.CreateTextFile("Site.url", "[InternetShortcut]\r\nURL=https://example.com/\r\nIconFile=https://example.com/favicon.ico\r\n");
        var missingIcon = testFolder.CreateTextFile("Missing.url", "[InternetShortcut]\r\nURL=steam://rungameid/1\r\nIconFile=C:\\Nowhere\\missing.ico\r\n");

        Assert.IsNull(ShortcutReader.ReadIcon(webIcon.FullName));
        Assert.IsNull(ShortcutReader.ReadIcon(missingIcon.FullName));
    }

    [TestMethod]
    public async Task FindAsync_Shortcut_IsACandidateNamedAfterIt()
    {
        using var testFolder = new TestFolder();
        var hades = testFolder.CreateFolder("Hades");
        var icon = testFolder.CreateFile(@"Steam\steam\games\5f3a.ico", ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!);
        var shortcut = testFolder.CreateTextFile(@"Hades\Hades.url", $"[InternetShortcut]\r\nURL=steam://rungameid/1145360\r\nIconFile={icon.FullName}\r\n");

        var candidates = await CandidateFinder.FindAsync(hades, ["Hades"], CancellationToken.None);

        Assert.HasCount(1, candidates);
        Assert.AreEqual("Hades.url", candidates[0].RelativePath);
        Assert.AreEqual(shortcut.FullName, candidates[0].ShortcutPath);
        Assert.AreEqual(new IconSource(icon.FullName, IconSourceKind.IconFile), candidates[0].Source);
    }

    [TestMethod]
    public async Task FindAsync_ShortcutToAFileFoundAnyway_IsLeftOut()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Game\bin\Game.exe");
        testFolder.CreateShortcut(@"Game\Game.lnk", exe.FullName);
        testFolder.CreateShortcut(@"Game\Play.lnk", exe.FullName);

        var candidates = await CandidateFinder.FindAsync(game, ["Game"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { @"bin\Game.exe" }, candidates.Select(candidate => candidate.RelativePath).Distinct().ToArray());
    }

    [TestMethod]
    public async Task FindAsync_ShortcutsToTheSameIcon_KeepTheBestNamed()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Elsewhere\launcher.exe");
        testFolder.CreateShortcut(@"Game\Play.lnk", exe.FullName);
        testFolder.CreateShortcut(@"Game\Game.lnk", exe.FullName);

        var candidates = await CandidateFinder.FindAsync(game, ["Game"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Game.lnk" }, candidates.Select(candidate => candidate.RelativePath).Distinct().ToArray());
    }

    [TestMethod]
    public async Task FindAsync_ShortcutToAHelper_IsLeftOut()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Elsewhere\Game.exe");
        var uninstaller = testFolder.CreateIconExe(@"Elsewhere\unins000.exe");
        testFolder.CreateShortcut(@"Game\Uninstall Game.lnk", exe.FullName);
        testFolder.CreateShortcut(@"Game\Remove.lnk", uninstaller.FullName);

        var candidates = await CandidateFinder.FindAsync(game, ["Game"], CancellationToken.None);

        Assert.IsEmpty(candidates);
    }

    [TestMethod]
    public void Rank_ShortcutNamedLikeTheFolder_CountsAsNamedLikeIt()
    {
        // Its icon file has an unrelated name, as Steam's do.
        var shortcut = new IconCandidate(new IconSource(@"C:\Steam\steam\games\5f3a.ico", IconSourceKind.IconFile), "Hades.url", 0, 50_000, 256, 256, ShortcutPath: @"C:\Games\Hades\Hades.url");
        var engine = new IconCandidate(new IconSource(@"C:\Games\Hades\Engine.exe", IconSourceKind.Module), "Engine.exe", 0, 5_000_000, 256, 256);

        Assert.AreSame(shortcut, CandidateRanker.Rank([engine, shortcut], ["Hades"])[0]);
    }

    [TestMethod]
    public void Rank_ShortcutToASecondaryIcon_CountsAsAMainIcon()
    {
        // The shortcut's icon was picked to stand for the app, so it isn't held behind the executable's blurry main icon, as a secondary icon would be.
        var exePath = @"C:\Apps\Tool\tool.exe";
        var mainIcon = new IconCandidate(new IconSource(exePath, IconSourceKind.Module, 0), "tool.exe", 0, 1_000_000, 32, 32);
        var shortcut = new IconCandidate(new IconSource(exePath, IconSourceKind.Module, 3), "Tool.lnk", 0, 1_000_000, 256, 256, ShortcutPath: @"C:\Apps\Tool\Tool.lnk");

        Assert.AreSame(shortcut, CandidateRanker.Rank([mainIcon, shortcut], ["Tool"])[0]);
    }
}

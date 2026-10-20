using IconPicker.Core.Candidates;

namespace IconPicker.Core.Tests;

/// <summary>Dropped or pasted paths, sorted into a folder to open and files to take icons from.</summary>
[TestClass]
public sealed class IconFileSelectionTests
{
    [TestMethod]
    public void FromPaths_SortsFoldersIconFilesAndTheRest()
    {
        using var testFolder = new TestFolder();
        var games = testFolder.CreateFolder("Games");
        var exe = testFolder.CreateIconExe(@"Games\Game.exe");
        var picture = testFolder.CreateFile(@"Games\cover.png", [1, 2, 3]);
        var text = testFolder.CreateTextFile(@"Games\readme.txt", "Read me");
        var folderShortcut = testFolder.CreateShortcut("Games.lnk", games.FullName);
        var exeShortcut = testFolder.CreateShortcut("Game.lnk", exe.FullName);

        var selection = IconFileSelection.FromPaths([text.FullName, folderShortcut.FullName, exe.FullName, Path.Combine(games.FullName, "missing.ico"), picture.FullName, exeShortcut.FullName, games.FullName]);

        Assert.AreEqual(folderShortcut.FullName, selection.FolderPath);
        CollectionAssert.AreEqual(new[] { exe.FullName, picture.FullName, exeShortcut.FullName }, selection.IconFiles.ToArray());
    }

    [TestMethod]
    public void ParsePathText_TakesQuotedAndPlainPathsPerLine()
    {
        var text = string.Join("\r\n", @"""C:\Games\Game.exe""", "", @"  D:\Icons\star.png  ", @"%SystemRoot%\explorer.exe", @"icons\relative.ico", "");

        var paths = IconFileSelection.ParsePathText(text);

        CollectionAssert.AreEqual(new[] { @"C:\Games\Game.exe", @"D:\Icons\star.png", Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "explorer.exe") }, paths.ToArray());
    }

    [TestMethod]
    public void ParsePathText_OtherText_GivesNoPaths()
    {
        Assert.IsEmpty(IconFileSelection.ParsePathText("Just a sentence, copied from somewhere."));
        Assert.IsEmpty(IconFileSelection.ParsePathText(""));
    }
}

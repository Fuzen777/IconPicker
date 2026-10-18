using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Detection;
using IconPicker.Core.Folders;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Tests;

/// <summary>Shortcuts to folders as targets: listed as rows, and given the folder's icon along with the folder.</summary>
[TestClass]
public sealed class FolderShortcutTests
{
    [TestMethod]
    public void ReadFolderTarget_OnlyForShortcutsToExistingFolders()
    {
        using var testFolder = new TestFolder();
        var library = testFolder.CreateFolder(@"Library\common");
        var exe = testFolder.CreateIconExe(@"Apps\Game.exe");
        var toFolder = testFolder.CreateShortcut(@"Games\Steam.lnk", library.FullName);
        var toExe = testFolder.CreateShortcut(@"Games\Game.lnk", exe.FullName);
        var toDrive = testFolder.CreateShortcut(@"Games\Drive.lnk", Path.GetPathRoot(testFolder.Root.FullName)!);
        var gone = testFolder.CreateFolder("Gone");
        var toDeleted = testFolder.CreateShortcut(@"Games\Deleted.lnk", gone.FullName);
        gone.Delete();

        Assert.AreEqual(library.FullName, ShortcutReader.ReadFolderTarget(toFolder.FullName)?.FullName);
        Assert.IsNull(ShortcutReader.ReadFolderTarget(toExe.FullName));
        // A drive can't take a folder icon.
        Assert.IsNull(ShortcutReader.ReadFolderTarget(toDrive.FullName));
        Assert.IsNull(ShortcutReader.ReadFolderTarget(toDeleted.FullName));
    }

    [TestMethod]
    public void GetDirectRows_ListsShortcutsToFoldersAmongTheSubfolders()
    {
        using var testFolder = new TestFolder();
        var games = testFolder.CreateFolder("Games");
        testFolder.CreateFolder(@"Games\Bluestacks");
        testFolder.CreateFolder(@"Games\Zelda");
        var library = testFolder.CreateFolder(@"SteamLibrary\common");
        testFolder.CreateShortcut(@"Games\Steam.lnk", library.FullName);
        testFolder.CreateShortcut(@"Games\Game.lnk", testFolder.CreateIconExe(@"Apps\Game.exe").FullName);
        var hidden = testFolder.CreateShortcut(@"Games\Hidden.lnk", library.FullName);
        hidden.Attributes |= FileAttributes.Hidden;

        var rows = BatchRowSource.GetDirectRows(games);

        CollectionAssert.AreEqual(new[] { "Bluestacks", "Steam", "Zelda" }, rows.Select(row => row.Name).ToArray());
        var steam = rows[1];
        Assert.AreEqual(library.FullName, steam.Folder.FullName);
        Assert.AreEqual(Path.Combine(games.FullName, "Steam.lnk"), steam.Shortcut?.FullName);
        CollectionAssert.AreEqual(new[] { "Steam", "common" }, steam.SearchNames.ToArray());
    }

    [TestMethod]
    public async Task ApplyAsync_ThroughAShortcut_GivesTheFolderItsIconAndPointsTheShortcutAtIt()
    {
        using var testFolder = new TestFolder();
        var library = testFolder.CreateFolder(@"SteamLibrary\common");
        var exe = testFolder.CreateIconExe(@"Apps\Steam.exe");
        var shortcut = testFolder.CreateShortcut(@"Games\Steam.lnk", library.FullName);
        // The folder got its icon on its own first: the shortcut doesn't point at it yet.
        await IconApplyService.ApplyAsync([new IconChange(library.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module))], IconTarget.Copy);
        var before = FolderIconState.Read(library, shortcut.FullName);

        var outcomes = await IconApplyService.ApplyAsync([new IconChange(library.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module), shortcut.FullName)], IconTarget.Copy);

        Assert.IsTrue(before is { IsMissingFromShortcut: true, NeedsIcon: true });
        Assert.AreEqual(IconChangeResult.Applied, outcomes.Single().Result);
        Assert.AreEqual(shortcut.FullName, outcomes.Single().Change.ItemPath);
        var after = FolderIconState.Read(library, shortcut.FullName);
        Assert.IsTrue(after is { Kind: FolderIconKind.Copied, NeedsIcon: false });
        Assert.AreEqual(new ShortcutIcon(Path.Combine(library.FullName, FolderIconWriter.IconFileName), 0), ShortcutReader.ReadOwnIcon(shortcut.FullName));
        Assert.AreEqual(library.FullName, ShortcutReader.ReadFolderTarget(shortcut.FullName)?.FullName);
    }

    [TestMethod]
    public async Task ApplyAsync_RemoveThroughAShortcut_TakesOffOnlyTheFolderIcon()
    {
        using var testFolder = new TestFolder();
        var library = testFolder.CreateFolder(@"SteamLibrary\common");
        var other = testFolder.CreateFolder("Other");
        var exe = testFolder.CreateIconExe(@"Apps\Steam.exe");
        var shortcut = testFolder.CreateShortcut(@"Games\Steam.lnk", library.FullName);
        var handSet = testFolder.CreateShortcut(@"Games\Other.lnk", other.FullName, $"{exe.FullName},0");
        await IconApplyService.ApplyAsync(
            [
                new IconChange(library.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module), shortcut.FullName),
                new IconChange(other.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module)),
            ], IconTarget.Copy);

        var outcomes = await IconApplyService.ApplyAsync(
            [
                new IconChange(library.FullName, IconChangeKind.Remove, ShortcutPath: shortcut.FullName),
                new IconChange(other.FullName, IconChangeKind.Remove, ShortcutPath: handSet.FullName),
            ], IconTarget.Copy);

        Assert.IsTrue(outcomes.All(outcome => outcome.Result == IconChangeResult.Applied));
        Assert.AreEqual(FolderIconKind.None, FolderIconState.Read(library).Kind);
        Assert.IsNull(ShortcutReader.ReadOwnIcon(shortcut.FullName));
        // An icon given to the shortcut by hand, from outside the folder, stays.
        Assert.AreEqual(new ShortcutIcon(exe.FullName, 0), ShortcutReader.ReadOwnIcon(handSet.FullName));
    }

    [TestMethod]
    public async Task ElevatedJob_ChangesShortcutsToFoldersOnly()
    {
        using var testFolder = new TestFolder();
        var library = testFolder.CreateFolder(@"SteamLibrary\common");
        var exe = testFolder.CreateIconExe(@"Apps\Steam.exe");
        var shortcut = testFolder.CreateShortcut(@"Games\Steam.lnk", library.FullName);
        var toExe = testFolder.CreateShortcut(@"Games\App.lnk", exe.FullName);
        var source = new IconSource(exe.FullName, IconSourceKind.Module);
        var jobPath = Path.Combine(testFolder.Root.FullName, "job.json");
        await ElevatedApplyJob.WriteJobAsync(jobPath, new ApplyJob(IconTarget.Copy,
            [
                new IconChange(library.FullName, IconChangeKind.Set, source, shortcut.FullName),
                new IconChange(library.FullName, IconChangeKind.Set, source, toExe.FullName),
            ]));

        await ElevatedApplyJob.RunAsync(jobPath);
        var result = await ElevatedApplyJob.ReadResultAsync(jobPath);

        Assert.AreEqual(IconChangeResult.Applied, result!.Outcomes.Single(outcome => outcome.Change.ShortcutPath == shortcut.FullName).Result);
        Assert.AreEqual(IconChangeResult.Failed, result.Outcomes.Single(outcome => outcome.Change.ShortcutPath == toExe.FullName).Result);
        Assert.IsNotNull(ShortcutReader.ReadOwnIcon(shortcut.FullName));
        Assert.IsNull(ShortcutReader.ReadOwnIcon(toExe.FullName));
    }
}

using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class ApplyingTests
{
    [TestMethod]
    public async Task ResolveAsync_LinkSetting_LinksFilesInsideTheFolder()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Game\bin\Game.exe");

        var plan = await IconTargetResolver.ResolveAsync(game, new IconSource(exe.FullName, IconSourceKind.Module, 0), IconTarget.Link, CancellationToken.None);

        Assert.AreEqual(@"bin\Game.exe,0", plan.IconSetting);
        Assert.IsNull(plan.IconFileBytes);
    }

    [TestMethod]
    public async Task ResolveAsync_CopiesWhenALinkIsntPossible()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Game\Game.exe");
        var picture = testFolder.CreateFile(@"Game\cover.png", await TestImages.CreatePngAsync(64, 64));
        var outsideExe = testFolder.CreateIconExe(@"Elsewhere\Other.exe");

        var copySetting = await IconTargetResolver.ResolveAsync(game, new IconSource(exe.FullName, IconSourceKind.Module), IconTarget.Copy, CancellationToken.None);
        var pictureWithLinkSetting = await IconTargetResolver.ResolveAsync(game, new IconSource(picture.FullName, IconSourceKind.Image), IconTarget.Link, CancellationToken.None);
        var outsideWithLinkSetting = await IconTargetResolver.ResolveAsync(game, new IconSource(outsideExe.FullName, IconSourceKind.Module), IconTarget.Link, CancellationToken.None);

        foreach (var plan in new[] { copySetting, pictureWithLinkSetting, outsideWithLinkSetting })
        {
            Assert.AreEqual(FolderIconWriter.CopiedIconSetting, plan.IconSetting);
            Assert.IsNotNull(IcoFileBuilder.ReadImageSizes(plan.IconFileBytes!));
        }
    }

    [TestMethod]
    public void LinksIntoVersionFolder_OnlyForLinksThroughAVersionFolder()
    {
        using var testFolder = new TestFolder();
        var updater = testFolder.CreateFolder("Updater");
        var versionedExe = new IconSource(testFolder.CreateIconExe(@"Updater\app-1.2.3\Updater.exe").FullName, IconSourceKind.Module);
        var plainExe = new IconSource(testFolder.CreateIconExe(@"Updater\Updater.exe").FullName, IconSourceKind.Module);

        Assert.IsTrue(IconTargetResolver.LinksIntoVersionFolder(updater, versionedExe, IconTarget.Link));
        Assert.IsFalse(IconTargetResolver.LinksIntoVersionFolder(updater, versionedExe, IconTarget.Copy));
        Assert.IsFalse(IconTargetResolver.LinksIntoVersionFolder(updater, plainExe, IconTarget.Link));
    }

    [TestMethod]
    public async Task ResolveAsync_ExeWithoutIcon_ReportsTheProblem()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var fakeExe = testFolder.CreateTextFile(@"Game\fake.exe", "not an executable");

        var exception = await Assert.ThrowsExactlyAsync<IconSourceException>(() => IconTargetResolver.ResolveAsync(game, new IconSource(fakeExe.FullName, IconSourceKind.Module), IconTarget.Copy, CancellationToken.None));

        Assert.AreEqual(IconSourceProblem.NoIcon, exception.Problem);
    }

    [TestMethod]
    public async Task ApplyAsync_ReportsEachOutcome()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var empty = testFolder.CreateFolder("Empty");
        var exe = testFolder.CreateIconExe(@"Game\Game.exe");
        var fakeExe = testFolder.CreateTextFile(@"Game\fake.exe", "not an executable");

        var outcomes = await IconApplyService.ApplyAsync(
            [
                new IconChange(game.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module)),
                new IconChange(game.FullName, IconChangeKind.Set, new IconSource(fakeExe.FullName, IconSourceKind.Module)),
                new IconChange(empty.FullName, IconChangeKind.Remove),
            ],
            IconTarget.Copy);

        CollectionAssert.AreEqual(new[] { IconChangeResult.Applied, IconChangeResult.Failed, IconChangeResult.Unchanged }, outcomes.Select(outcome => outcome.Result).ToArray());
        Assert.AreEqual(IconSourceProblem.NoIcon, outcomes[1].Problem);
        Assert.AreEqual(FolderIconKind.Copied, FolderIconState.Read(game).Kind);
    }

    [TestMethod]
    public async Task ElevatedJob_AppliesValidChangesAndReportsTheRest()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Game\Game.exe");
        var jobPath = Path.Combine(testFolder.Root.FullName, "job.json");
        var missingFolder = Path.Combine(testFolder.Root.FullName, "Missing");
        await ElevatedApplyJob.WriteJobAsync(jobPath, new ApplyJob(IconTarget.Link,
            [
                new IconChange(game.FullName, IconChangeKind.Set, new IconSource(exe.FullName, IconSourceKind.Module)),
                new IconChange(missingFolder, IconChangeKind.Remove),
            ]));

        var exitCode = await ElevatedApplyJob.RunAsync(jobPath);
        var result = await ElevatedApplyJob.ReadResultAsync(jobPath);

        Assert.AreEqual(0, exitCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(IconChangeResult.Applied, result.Outcomes.Single(outcome => outcome.Change.FolderPath == game.FullName).Result);
        Assert.AreEqual(IconChangeResult.Failed, result.Outcomes.Single(outcome => outcome.Change.FolderPath == missingFolder).Result);
        Assert.AreEqual(FolderIconKind.Linked, FolderIconState.Read(game).Kind);
    }
}

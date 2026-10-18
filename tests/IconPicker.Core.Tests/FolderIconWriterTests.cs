using IconPicker.Core.Folders;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class FolderIconWriterTests
{
    private static readonly byte[] SampleIconFile = ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!;

    [TestMethod]
    public void SetIcon_Copy_WritesHiddenIconAndDesktopIniAndMarksTheFolder()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");

        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile));

        var iconFilePath = Path.Combine(game.FullName, FolderIconWriter.IconFileName);
        CollectionAssert.AreEqual(SampleIconFile, File.ReadAllBytes(iconFilePath));
        Assert.IsTrue(File.GetAttributes(iconFilePath).HasFlag(FileAttributes.Hidden | FileAttributes.System));
        game.Refresh();
        Assert.IsTrue(game.Attributes.HasFlag(FileAttributes.ReadOnly));
        var state = FolderIconState.Read(game);
        Assert.AreEqual(FolderIconKind.Copied, state.Kind);
        Assert.IsFalse(state.IsBroken);
        Assert.AreEqual(FolderIconWriter.CopiedIconSetting, state.IconSetting);
    }

    [TestMethod]
    public void Read_FolderFromAnEarlierListing_SeesTheNewIcon()
    {
        using var testFolder = new TestFolder();
        testFolder.CreateFolder("Game");
        // Listed before the icon is set, so its cached attributes lack ReadOnly.
        var listedFolder = testFolder.Root.GetDirectories().Single();
        _ = listedFolder.Attributes;

        FolderIconWriter.SetIcon(new DirectoryInfo(listedFolder.FullName), IconWritePlan.Copy(SampleIconFile));

        Assert.IsFalse(FolderIconState.Read(listedFolder).IsBroken);
    }

    [TestMethod]
    public void SetIcon_LinkAfterCopy_DeletesTheCopy()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var exe = testFolder.CreateIconExe(@"Game\bin\Game.exe");
        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile));

        FolderIconWriter.SetIcon(game, IconWritePlan.Link(@"bin\Game.exe", 0));

        Assert.IsFalse(File.Exists(Path.Combine(game.FullName, FolderIconWriter.IconFileName)));
        var state = FolderIconState.Read(game);
        Assert.AreEqual(FolderIconKind.Linked, state.Kind);
        Assert.AreEqual(exe.FullName, state.IconPath);
        Assert.AreEqual(0, state.IconIndex);
        Assert.IsFalse(state.IsBroken);
    }

    [TestMethod]
    public void RemoveIcon_Copy_DeletesEverythingItAdded()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile));

        Assert.IsTrue(FolderIconWriter.RemoveIcon(game));

        Assert.IsFalse(File.Exists(Path.Combine(game.FullName, FolderIconWriter.IconFileName)));
        Assert.IsFalse(File.Exists(Path.Combine(game.FullName, DesktopIniFile.FileName)));
        game.Refresh();
        Assert.IsFalse(game.Attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.AreEqual(FolderIconState.NoIcon, FolderIconState.Read(game));
    }

    [TestMethod]
    public void RemoveIcon_KeepsOtherSettingsAndTheFolderAttribute()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        testFolder.CreateTextFile(@"Game\desktop.ini", "[.ShellClassInfo]\r\nLocalizedResourceName=Jeu\r\n");
        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile));

        FolderIconWriter.RemoveIcon(game);

        var desktopIni = DesktopIniFile.Read(game.FullName);
        Assert.AreEqual("Jeu", desktopIni.GetValue("LocalizedResourceName"));
        Assert.IsNull(desktopIni.GetIconSetting());
        game.Refresh();
        Assert.IsTrue(game.Attributes.HasFlag(FileAttributes.ReadOnly));
    }

    [TestMethod]
    public void RemoveIcon_External_KeepsTheIconFile()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var externalIcon = testFolder.CreateFile(@"Icons\game.ico", SampleIconFile);
        testFolder.CreateTextFile(@"Game\desktop.ini", $"[.ShellClassInfo]\r\nIconResource={externalIcon.FullName},0\r\n");
        game.Attributes |= FileAttributes.ReadOnly;
        Assert.AreEqual(FolderIconKind.External, FolderIconState.Read(game).Kind);

        FolderIconWriter.RemoveIcon(game);

        Assert.IsTrue(File.Exists(externalIcon.FullName));
        Assert.AreEqual(FolderIconState.NoIcon, FolderIconState.Read(game));
    }

    [TestMethod]
    public void ScriptIcon_IsRecognisedAsACopy()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        testFolder.CreateFile(@"Game\folder-icon.ico", SampleIconFile);
        File.WriteAllText(Path.Combine(game.FullName, DesktopIniFile.FileName), "[.ShellClassInfo]\r\nIconResource=folder-icon.ico,0\r\n", System.Text.Encoding.Unicode);
        game.Attributes |= FileAttributes.ReadOnly;

        var state = FolderIconState.Read(game);

        Assert.AreEqual(FolderIconKind.Copied, state.Kind);
        Assert.IsFalse(state.IsBroken);
        Assert.IsFalse(state.NeedsIcon);
    }

    [TestMethod]
    public void CopyWithMissingFile_IsBroken()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile));
        var iconFilePath = Path.Combine(game.FullName, FolderIconWriter.IconFileName);
        File.SetAttributes(iconFilePath, FileAttributes.Normal);
        File.Delete(iconFilePath);

        var state = FolderIconState.Read(game);

        Assert.AreEqual(FolderIconKind.Copied, state.Kind);
        Assert.IsTrue(state.IsBroken);
        Assert.IsTrue(state.NeedsIcon);
    }

    [TestMethod]
    public void SetIcon_Failure_LeavesNoHalfMadeIcon()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        // A folder named desktop.ini can't be written as a file, so the change fails after the icon file was written.
        testFolder.CreateFolder(@"Game\desktop.ini");

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => FolderIconWriter.SetIcon(game, IconWritePlan.Copy(SampleIconFile)));

        Assert.IsFalse(File.Exists(Path.Combine(game.FullName, FolderIconWriter.IconFileName)));
        game.Refresh();
        Assert.IsFalse(game.Attributes.HasFlag(FileAttributes.ReadOnly));
    }
}

using IconPicker.Core.Rules;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class FolderSearchRulesTests
{
    [TestMethod]
    [DataRow("unins000.exe", true)]
    [DataRow("UnityCrashHandler64.exe", true)]
    [DataRow("Setup.exe", true)]
    [DataRow("GameInstaller.exe", true)]
    [DataRow("EasyAntiCheat_EOS_Setup.exe", true)]
    [DataRow("Game.exe", false)]
    [DataRow("Launcher.exe", false)]
    public void IsIgnoredExe_MatchesTheScriptPatterns(string exeFileName, bool isIgnored)
    {
        Assert.AreEqual(isIgnored, FolderSearchRules.IsIgnoredExe(exeFileName));
    }

    [TestMethod]
    [DataRow("1.2.3", true)]
    [DataRow("v1.2", true)]
    [DataRow("app-1.2.3", true)]
    [DataRow("1", false)]
    [DataRow("version1", false)]
    [DataRow("Data", false)]
    public void IsVersionFolderName_MatchesVersionFolders(string folderName, bool isVersionFolder)
    {
        Assert.AreEqual(isVersionFolder, FolderSearchRules.IsVersionFolderName(folderName));
    }

    [TestMethod]
    [DataRow("bin", true)]
    [DataRow("Binaries", true)]
    [DataRow("Win64", true)]
    [DataRow("app-2.0.1", true)]
    [DataRow("Game", true)]
    [DataRow("Data", false)]
    public void IsBinStyleFolder_MatchesBinFoldersAndTheParentName(string subFolderName, bool isBinStyle)
    {
        var parentFolder = new DirectoryInfo(@"C:\Games\Game");
        var subFolder = new DirectoryInfo(Path.Combine(parentFolder.FullName, subFolderName));

        Assert.AreEqual(isBinStyle, FolderSearchRules.IsBinStyleFolder(subFolder, parentFolder));
    }

    [TestMethod]
    public void IsSkippedFolder_SkipsDeveloperFoldersButNotRepositories()
    {
        using var testFolder = new TestFolder();
        var nodeModules = testFolder.CreateFolder("node_modules");
        var repository = testFolder.CreateFolder("Project");
        testFolder.CreateFolder(@"Project\.git");
        var game = testFolder.CreateFolder("Game");

        Assert.IsTrue(FolderSearchRules.IsSkippedFolder(nodeModules));
        Assert.IsFalse(FolderSearchRules.IsSkippedFolder(repository));
        Assert.IsTrue(FolderSearchRules.IsRepository(repository));
        Assert.IsFalse(FolderSearchRules.IsSkippedFolder(game));
        Assert.IsFalse(FolderSearchRules.IsRepository(game));
    }

    [TestMethod]
    public void IsSkippedCandidateFolder_SkipsRepositoriesNestedInAFolder()
    {
        using var testFolder = new TestFolder();
        var nestedRepository = testFolder.CreateFolder(@"Game\Tools\Modkit");
        testFolder.CreateFolder(@"Game\Tools\Modkit\.git");

        Assert.IsTrue(FolderSearchRules.IsSkippedCandidateFolder(nestedRepository));
    }

    [TestMethod]
    public void IsSkippedCandidateFolder_AlsoSkipsSteamLibraries()
    {
        using var testFolder = new TestFolder();
        var steamApps = testFolder.CreateFolder(@"Steam\steamapps");

        Assert.IsFalse(FolderSearchRules.IsSkippedFolder(steamApps));
        Assert.IsTrue(FolderSearchRules.IsSkippedCandidateFolder(steamApps));
    }

    [TestMethod]
    public void IsCloudPlaceholder_DetectsFilesThatArentOnThisPC()
    {
        Assert.IsFalse(FolderSearchRules.IsCloudPlaceholder(FileAttributes.Archive));
        Assert.IsTrue(FolderSearchRules.IsCloudPlaceholder(FileAttributes.Offline));
        Assert.IsTrue(FolderSearchRules.IsCloudPlaceholder(FileAttributes.Archive | (FileAttributes)0x00400000));
        Assert.IsTrue(FolderSearchRules.IsCloudPlaceholder((FileAttributes)0x00040000));
    }
}

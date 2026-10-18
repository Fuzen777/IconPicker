using IconPicker.Core.Launch;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class LaunchArgumentsTests
{
    [TestMethod]
    public void Parse_WithoutArguments_ReturnsNone()
    {
        Assert.AreEqual(LaunchArguments.None, LaunchArguments.Parse(@"""C:\Apps\Icon Picker\IconPicker.exe"""));
        Assert.AreEqual(LaunchArguments.None, LaunchArguments.Parse(""));
        Assert.AreEqual(LaunchArguments.None, LaunchArguments.Parse(null));
    }

    [TestMethod]
    public void Parse_FolderAfterProgramPath_ReturnsFolder()
    {
        var launchArguments = LaunchArguments.Parse(@"""C:\Apps\Icon Picker\IconPicker.exe"" ""D:\Games\Some Game""");

        Assert.AreEqual(@"D:\Games\Some Game", launchArguments.FolderPath);
        Assert.IsNull(launchArguments.ElevatedJobPath);
    }

    [TestMethod]
    public void Parse_FolderWithoutProgramPath_ReturnsFolder()
    {
        Assert.AreEqual(@"D:\Games", LaunchArguments.Parse(@"""D:\Games""").FolderPath);
    }

    [TestMethod]
    public void Parse_QuotedDriveRoot_KeepsTheBackslash()
    {
        // Explorer's "%V" for a drive gives "D:\", which the standard Windows rules turn into D:".
        Assert.AreEqual(@"D:\", LaunchArguments.Parse(@"""C:\Apps\IconPicker.exe"" ""D:\""").FolderPath);
    }

    [TestMethod]
    public void Parse_DriveLetterAlone_MeansTheDriveRoot()
    {
        Assert.AreEqual(@"D:\", LaunchArguments.Parse("IconPicker.exe D:").FolderPath);
    }

    [TestMethod]
    public void Parse_TrailingDot_IsNormalized()
    {
        Assert.AreEqual(@"D:\", LaunchArguments.Parse(@"IconPicker.exe ""D:\.""").FolderPath);
    }

    [TestMethod]
    public void Parse_ElevatedJob_ReturnsJobPath()
    {
        var launchArguments = LaunchArguments.Parse(@"""C:\Apps\IconPicker.exe"" --apply-elevated ""C:\Users\Me\AppData\Local\Icon Picker\jobs\1.json""");

        Assert.AreEqual(@"C:\Users\Me\AppData\Local\Icon Picker\jobs\1.json", launchArguments.ElevatedJobPath);
        Assert.IsNull(launchArguments.FolderPath);
    }

    [TestMethod]
    public void Parse_ElevatedSwitchWithoutJob_ReturnsNone()
    {
        Assert.AreEqual(LaunchArguments.None, LaunchArguments.Parse("IconPicker.exe --apply-elevated"));
    }
}

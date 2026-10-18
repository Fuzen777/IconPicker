using IconPicker.Core.Launch;
using IconPicker.Core.Storage;

namespace IconPicker.Core.Tests;

/// <summary>Moving the app's data between %LocalAppData% and a portable Data folder.</summary>
[TestClass]
public sealed class DataFolderMoverTests
{
    [TestMethod]
    public void Move_IntoAMissingFolder_MovesEverythingButTheJobs()
    {
        using var testFolder = new TestFolder();
        var source = testFolder.CreateFolder(@"AppData\Icon Picker").FullName;
        testFolder.CreateTextFile(@"AppData\Icon Picker\settings.json", "{}");
        testFolder.CreateTextFile(@"AppData\Icon Picker\settings.json.tmp", "half");
        testFolder.CreateTextFile(@"AppData\Icon Picker\My icons\abc\recipe.json", "recipe");
        testFolder.CreateTextFile(@"AppData\Icon Picker\jobs\job.json", "job");
        var readOnlyIcon = testFolder.CreateTextFile(@"AppData\Icon Picker\My icons\abc\icon.ico", "icon");
        readOnlyIcon.Attributes |= FileAttributes.ReadOnly;
        var destination = Path.Combine(testFolder.Root.FullName, @"App\Data");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        DataFolderMover.Move(source, destination);

        Assert.IsFalse(Directory.Exists(source));
        Assert.AreEqual("{}", File.ReadAllText(Path.Combine(destination, "settings.json")));
        Assert.AreEqual("recipe", File.ReadAllText(Path.Combine(destination, @"My icons\abc\recipe.json")));
        Assert.IsTrue(File.Exists(Path.Combine(destination, @"My icons\abc\icon.ico")));
        Assert.IsFalse(Directory.Exists(Path.Combine(destination, DataFolderMover.JobsFolderName)));
        Assert.IsFalse(File.Exists(Path.Combine(destination, "settings.json.tmp")));
        // No leftovers under other names next to either folder.
        CollectionAssert.AreEqual(new[] { "Data" }, Directory.GetDirectories(Path.GetDirectoryName(destination)!).Select(Path.GetFileName).ToArray());
        Assert.IsEmpty(Directory.GetDirectories(Path.GetDirectoryName(source)!));
    }

    [TestMethod]
    public void Move_WithNothingToMove_StillMakesTheDestination()
    {
        // Turning portable on before the app saved anything still has to leave a Data folder behind.
        using var testFolder = new TestFolder();
        var destination = Path.Combine(testFolder.Root.FullName, "Data");

        DataFolderMover.Move(Path.Combine(testFolder.Root.FullName, "Missing"), destination);

        Assert.IsTrue(Directory.Exists(destination));
    }

    [TestMethod]
    public void Move_IntoAnExistingFolder_MergesAndJoinsTheErrorLogs()
    {
        using var testFolder = new TestFolder();
        var source = testFolder.CreateFolder("Data").FullName;
        testFolder.CreateTextFile(@"Data\settings.json", "moved");
        testFolder.CreateTextFile(@"Data\error.log", "second\n");
        testFolder.CreateTextFile(@"Data\My icons\new\recipe.json", "new");
        var destination = testFolder.CreateFolder("Icon Picker").FullName;
        testFolder.CreateTextFile(@"Icon Picker\settings.json", "old");
        testFolder.CreateTextFile(@"Icon Picker\error.log", "first\n");
        testFolder.CreateTextFile(@"Icon Picker\My icons\kept\recipe.json", "kept");

        DataFolderMover.Move(source, destination);

        Assert.IsFalse(Directory.Exists(source));
        Assert.AreEqual("moved", File.ReadAllText(Path.Combine(destination, "settings.json")));
        Assert.AreEqual("first\nsecond\n", File.ReadAllText(Path.Combine(destination, DataFolderMover.ErrorLogFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(destination, @"My icons\new\recipe.json")));
        Assert.IsTrue(File.Exists(Path.Combine(destination, @"My icons\kept\recipe.json")));
    }

    [TestMethod]
    public void Move_ThatFailsToCopy_LeavesTheSourceAndNoDestination()
    {
        using var testFolder = new TestFolder();
        var source = testFolder.CreateFolder(@"AppData\Icon Picker").FullName;
        var locked = testFolder.CreateTextFile(@"AppData\Icon Picker\settings.json", "{}");
        var destination = Path.Combine(testFolder.Root.FullName, "Data");

        using (File.Open(locked.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsExactly<IOException>(() => DataFolderMover.Move(source, destination));
        }

        Assert.IsTrue(File.Exists(locked.FullName));
        // A half-made Data folder would switch the app to it.
        Assert.IsFalse(Directory.Exists(destination));
    }

    [TestMethod]
    public void LaunchArguments_DataMove_RoundTrips()
    {
        var toPortable = new DataMoveRequest(true, 4321);
        var toAppData = new DataMoveRequest(false, 7);

        Assert.AreEqual(toPortable, LaunchArguments.Parse(@"""C:\Apps\IconPicker.exe"" " + LaunchArguments.FormatDataMove(toPortable)).DataMove);
        Assert.AreEqual(toAppData, LaunchArguments.Parse(LaunchArguments.FormatDataMove(toAppData)).DataMove);
        Assert.IsNull(LaunchArguments.Parse("--move-data elsewhere 12").DataMove);
        Assert.IsNull(LaunchArguments.Parse("--move-data portable").DataMove);
        Assert.IsNull(LaunchArguments.Parse("--move-data portable 12").FolderPath);
    }
}

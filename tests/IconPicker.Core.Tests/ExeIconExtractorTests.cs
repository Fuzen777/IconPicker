using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class ExeIconExtractorTests
{
    [TestMethod]
    public void CountIcons_ExeWithIcons_IsPositive()
    {
        Assert.IsGreaterThan(0, ExeIconExtractor.CountIcons(TestFolder.IconExePath));
    }

    [TestMethod]
    public void ExtractIconFile_FirstIcon_IsAValidIcoFile()
    {
        var iconFile = ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0);

        Assert.IsNotNull(iconFile);
        var sizes = IcoFileBuilder.ReadImageSizes(iconFile);
        Assert.IsNotNull(sizes);
        Assert.IsNotEmpty(sizes);
    }

    [TestMethod]
    public void ExtractIconFile_IndexPastTheLastIcon_ReturnsNull()
    {
        Assert.IsNull(ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 9999));
    }

    [TestMethod]
    public void ExtractIconFile_NegativeIndex_IsTheResourceId()
    {
        // dotnet.exe names its icon group with a string, so this needs a file whose groups are numbered.
        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var modulePath = new[] { "cmd.exe", "regedit.exe", "notepad.exe", "shell32.dll", "imageres.dll" }
            .Select(fileName => Path.Combine(systemFolder, fileName))
            .FirstOrDefault(path => ExeIconExtractor.GetIconGroupIds(path).Count > 0 && ExeIconExtractor.ExtractIconFile(path, 0) is not null);
        if (modulePath is null)
        {
            Assert.Inconclusive("No system file with numbered icon groups was found.");
        }
        var groupIds = ExeIconExtractor.GetIconGroupIds(modulePath);

        CollectionAssert.AreEqual(ExeIconExtractor.ExtractIconFile(modulePath, 0), ExeIconExtractor.ExtractIconFile(modulePath, -groupIds[0]));
    }

    [TestMethod]
    public void ListIcons_ReportsIndexAndLargestSize()
    {
        var icons = ExeIconExtractor.ListIcons(TestFolder.IconExePath);

        Assert.IsNotEmpty(icons);
        Assert.AreEqual(0, icons[0].Index);
        Assert.IsGreaterThanOrEqualTo(16, icons[0].LargestSize);
    }

    [TestMethod]
    public void FileThatIsntAnExecutable_HasNoIcons()
    {
        using var testFolder = new TestFolder();
        var fakeExe = testFolder.CreateTextFile("fake.exe", "not an executable");

        Assert.AreEqual(0, ExeIconExtractor.CountIcons(fakeExe.FullName));
        Assert.IsNull(ExeIconExtractor.ExtractIconFile(fakeExe.FullName, 0));
        Assert.IsEmpty(ExeIconExtractor.ListIcons(fakeExe.FullName));
    }
}

using System.Text;
using IconPicker.Core.Folders;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class DesktopIniFileTests
{
    [TestMethod]
    public void Read_MissingFile_IsEmptyUtf16()
    {
        using var testFolder = new TestFolder();

        var desktopIni = DesktopIniFile.Read(testFolder.Root.FullName);

        Assert.IsFalse(desktopIni.HasContent);
        Assert.AreEqual(Encoding.Unicode.CodePage, desktopIni.Encoding.CodePage);
        Assert.IsNull(desktopIni.GetIconSetting());
    }

    [TestMethod]
    public void SetIconResource_Utf16File_KeepsEncodingAndOtherSettings()
    {
        using var testFolder = new TestFolder();
        var filePath = Path.Combine(testFolder.Root.FullName, DesktopIniFile.FileName);
        File.WriteAllText(filePath, "[.ShellClassInfo]\r\nLocalizedResourceName=Jeux vidéo\r\nIconFile=C:\\old.ico\r\nIconIndex=2\r\n[ViewState]\r\nMode=\r\n", Encoding.Unicode);

        var desktopIni = DesktopIniFile.Read(testFolder.Root.FullName);
        desktopIni.SetIconResource("folder-icon.ico,0");
        desktopIni.Save();

        var bytes = File.ReadAllBytes(filePath);
        Assert.AreEqual(0xFF, bytes[0]);
        Assert.AreEqual(0xFE, bytes[1]);
        var text = File.ReadAllText(filePath, Encoding.Unicode);
        Assert.AreEqual("[.ShellClassInfo]\r\nIconResource=folder-icon.ico,0\r\nLocalizedResourceName=Jeux vidéo\r\n[ViewState]\r\nMode=\r\n", text);
        Assert.IsTrue(File.GetAttributes(filePath).HasFlag(FileAttributes.Hidden | FileAttributes.System));
    }

    [TestMethod]
    public void SetIconResource_AnsiFile_KeepsAccentedBytes()
    {
        using var testFolder = new TestFolder();
        var filePath = Path.Combine(testFolder.Root.FullName, DesktopIniFile.FileName);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansiEncoding = Encoding.GetEncoding(0);
        File.WriteAllBytes(filePath, ansiEncoding.GetBytes("[.ShellClassInfo]\r\nInfoTip=Créé\r\n"));

        var desktopIni = DesktopIniFile.Read(testFolder.Root.FullName);
        desktopIni.SetIconResource("folder-icon.ico,0");
        desktopIni.Save();

        CollectionAssert.AreEqual(ansiEncoding.GetBytes("[.ShellClassInfo]\r\nIconResource=folder-icon.ico,0\r\nInfoTip=Créé\r\n"), File.ReadAllBytes(filePath));
    }

    [TestMethod]
    public void SetIconResource_NoSection_AddsItAtTheTop()
    {
        using var testFolder = new TestFolder();
        testFolder.CreateTextFile(DesktopIniFile.FileName, "[ViewState]\r\nMode=\r\n");

        var desktopIni = DesktopIniFile.Read(testFolder.Root.FullName);
        desktopIni.SetIconResource("bin\\Game.exe,0");

        CollectionAssert.AreEqual(new[] { "[.ShellClassInfo]", "IconResource=bin\\Game.exe,0", "[ViewState]", "Mode=", "" }, desktopIni.Lines.ToArray());
        Assert.AreEqual(new DesktopIniSetting("IconResource", "bin\\Game.exe,0"), desktopIni.GetIconSetting());
    }

    [TestMethod]
    public void RemoveIconSettings_DropsTheEmptySection()
    {
        using var testFolder = new TestFolder();
        testFolder.CreateTextFile(DesktopIniFile.FileName, "[.ShellClassInfo]\r\nIconFile=x.ico\r\nIconIndex=0\r\n[ViewState]\r\nMode=\r\n");

        var desktopIni = DesktopIniFile.Read(testFolder.Root.FullName);

        Assert.IsTrue(desktopIni.RemoveIconSettings());
        CollectionAssert.AreEqual(new[] { "[ViewState]", "Mode=", "" }, desktopIni.Lines.ToArray());
        Assert.IsFalse(desktopIni.RemoveIconSettings());
    }
}

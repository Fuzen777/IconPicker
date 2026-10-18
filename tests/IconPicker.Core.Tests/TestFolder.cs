namespace IconPicker.Core.Tests;

/// <summary>A temporary folder tree for one test, deleted afterwards even when tests left hidden, system or read-only files in it.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "IconPickerTests", Guid.NewGuid().ToString("N")));
        Root.Create();
    }

    /// <summary>An executable that holds icons: the .NET host, present wherever these tests can run.</summary>
    public static string IconExePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");

    public DirectoryInfo Root { get; }

    public DirectoryInfo CreateFolder(string relativePath)
    {
        return Directory.CreateDirectory(Path.Combine(Root.FullName, relativePath));
    }

    /// <summary>A copy of <see cref="IconExePath"/> at the given relative path.</summary>
    public FileInfo CreateIconExe(string relativePath)
    {
        var filePath = Path.Combine(Root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.Copy(IconExePath, filePath);
        return new FileInfo(filePath);
    }

    public FileInfo CreateFile(string relativePath, byte[] content)
    {
        var filePath = Path.Combine(Root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, content);
        return new FileInfo(filePath);
    }

    public FileInfo CreateTextFile(string relativePath, string content)
    {
        return CreateFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
    }

    /// <summary>A .lnk shortcut to <paramref name="targetPath"/>, given its own icon when <paramref name="iconLocation"/> is set ("file,index"), written through Windows Script Host as Explorer would.</summary>
    public FileInfo CreateShortcut(string relativePath, string targetPath, string? iconLocation = null)
    {
        var filePath = Path.Combine(Root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        var shortcut = shell.CreateShortcut(filePath);
        shortcut.TargetPath = targetPath;
        if (iconLocation is not null)
        {
            shortcut.IconLocation = iconLocation;
        }
        shortcut.Save();
        return new FileInfo(filePath);
    }

    public void Dispose()
    {
        if (!Root.Exists)
        {
            return;
        }
        foreach (var entry in Root.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Append(Root))
        {
            entry.Attributes = entry is DirectoryInfo ? FileAttributes.Directory : FileAttributes.Normal;
        }
        Root.Delete(recursive: true);
    }
}

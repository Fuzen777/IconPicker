using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace IconPicker.Core.Rules;

/// <summary>Which folders are searched and which executables count, ported from Set Folder Icons.ps1.</summary>
public static partial class FolderSearchRules
{
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    // Executables that ship next to games and apps but aren't the program itself.
    private static readonly Regex[] IgnoredExePatterns = new[]
    {
        "unins*", "*install*", "*setup*", "*redist*", "dotnet*",
        "UnityCrashHandler*", "*CrashReport*", "*CrashHandler*", "*crashpad*", "BsSndRpt*",
        "EasyAntiCheat*", "BEService*", "QtWebEngineProcess*", "oalinst*", "PhysX*",
        "dpinst*", "mbuns*", "*UpdateChecker*", "busybox*",
    }.Select(WildcardToRegex).ToArray();

    // Never searched, wherever they are: redistributable installers, and developer tools and runtimes, whose many .exe files are internals rather than apps.
    private static readonly HashSet<string> SkippedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "_CommonRedist", "Steamworks Shared",
        "dotnet", "Microsoft SDKs", "Windows Kits", "MSBuild", "Microsoft Visual Studio", "packs", "NuGetPackages",
        "vfs", "site-packages", "Lib", "Scripts",
    };

    // Only skipped when looking for icon candidates: a Steam library holds games, never the source of Steam's own icon.
    private static readonly HashSet<string> SkippedCandidateFolderNames = new(StringComparer.OrdinalIgnoreCase) { "steamapps" };

    // Skipped at the top of a drive. Users is replaced by each user's AppData\Local\Programs, since Downloads and Desktop are full of loose installers.
    private static readonly HashSet<string> SkippedDriveRootFolderNames = new(StringComparer.OrdinalIgnoreCase) { "Windows", "Windows.old", "ProgramData" };
    private static readonly HashSet<string> ProgramFilesFolderNames = new(StringComparer.OrdinalIgnoreCase) { "Program Files", "Program Files (x86)" };

    // Owner of the folders Windows itself installs in Program Files (Windows Defender, WindowsApps...), which even admin can't change.
    private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    public static bool IsIgnoredExe(string exeFileName)
    {
        return IgnoredExePatterns.Any(pattern => pattern.IsMatch(exeFileName));
    }

    /// <summary>Subfolders whose .exe belongs to the folder above them: Game\bin\game.exe gives Game its icon. A subfolder named like its parent counts too (Game\Game\Binaries\Win64).</summary>
    public static bool IsBinStyleFolder(DirectoryInfo subFolder, DirectoryInfo parentFolder)
    {
        return BinStyleFolderPattern().IsMatch(subFolder.Name)
            || IsVersionFolderName(subFolder.Name)
            || string.Equals(subFolder.Name, parentFolder.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Version folders used by auto-updating apps: 1.2.3, v1.2, app-1.2.3. A link into one breaks at the next update.</summary>
    public static bool IsVersionFolderName(string folderName)
    {
        return VersionFolderPattern().IsMatch(folderName);
    }

    public static bool IsDriveRoot(DirectoryInfo? folder)
    {
        return folder is not null && folder.Parent is null;
    }

    /// <summary>The folder's subfolders sorted by name. Folders we aren't allowed to list are simply not searched.</summary>
    public static DirectoryInfo[] GetSubFolders(DirectoryInfo folder)
    {
        try
        {
            var subFolders = folder.GetDirectories();
            Array.Sort(subFolders, (first, second) => StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name));
            return subFolders;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            return [];
        }
    }

    public static bool IsSkippedFolder(DirectoryInfo folder)
    {
        var attributes = folder.Attributes;
        // Links and junctions can point back up the tree and loop forever.
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return true;
        }
        // $Recycle.Bin, System Volume Information and other Windows internals.
        if (attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.System))
        {
            return true;
        }
        if (SkippedFolderNames.Contains(folder.Name))
        {
            return true;
        }
        var parentFolder = folder.Parent;
        if (IsDriveRoot(parentFolder) && SkippedDriveRootFolderNames.Contains(folder.Name))
        {
            return true;
        }
        if (parentFolder is not null && ProgramFilesFolderNames.Contains(parentFolder.Name) && IsDriveRoot(parentFolder.Parent))
        {
            if (string.Equals(folder.Name, "Common Files", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (IsOwnedByWindows(folder))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A git repository. The script skipped repositories entirely; the app lists them like any folder (a downloaded tool is often a git clone), but doesn't search inside them for apps or icons, since the .exe files deep in a code project are build outputs.
    /// </summary>
    public static bool IsRepository(DirectoryInfo folder)
    {
        return Path.Exists(Path.Combine(folder.FullName, ".git"));
    }

    /// <summary>
    /// A hidden folder: tool-managed data such as Google Drive's .tmp.drivedownload, never a folder the user names an icon for. It gets no row in batch mode, but the deep search still walks through it, since apps can sit under hidden folders (AppData\Local\Programs).
    /// </summary>
    public static bool IsHiddenFolder(DirectoryInfo folder)
    {
        return folder.Attributes.HasFlag(FileAttributes.Hidden);
    }

    /// <summary>Skipped while looking for icon candidates inside a folder, including repositories nested in it.</summary>
    public static bool IsSkippedCandidateFolder(DirectoryInfo folder)
    {
        return SkippedCandidateFolderNames.Contains(folder.Name) || IsSkippedFolder(folder) || IsRepository(folder);
    }

    /// <summary>The subfolders worth searching for app folders. At the top of a drive, Users is replaced by each user's AppData\Local\Programs.</summary>
    public static IEnumerable<DirectoryInfo> GetFoldersToSearch(DirectoryInfo folder)
    {
        var isDriveRoot = IsDriveRoot(folder);
        foreach (var subFolder in GetSubFolders(folder))
        {
            if (isDriveRoot && string.Equals(subFolder.Name, "Users", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var userFolder in GetSubFolders(subFolder))
                {
                    if (userFolder.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                    var userProgramsPath = Path.Combine(userFolder.FullName, "AppData", "Local", "Programs");
                    if (Directory.Exists(userProgramsPath))
                    {
                        yield return new DirectoryInfo(userProgramsPath);
                    }
                }
            }
            else if (!IsSkippedFolder(subFolder))
            {
                yield return subFolder;
            }
        }
    }

    /// <summary>A cloud file (Google Drive, OneDrive) that isn't on this PC. Reading it would start a download.</summary>
    public static bool IsCloudPlaceholder(FileAttributes attributes)
    {
        return (attributes & (FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0;
    }

    internal static bool IsOwnedByWindows(DirectoryInfo folder)
    {
        try
        {
            var owner = folder.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
            return owner?.Value == TrustedInstallerSid;
        }
        catch (Exception)
        {
            // WindowsApps can't even be read, which makes it Windows' own as well.
            return true;
        }
    }

    private static Regex WildcardToRegex(string wildcardPattern)
    {
        var regexPattern = "^" + Regex.Escape(wildcardPattern).Replace(@"\*", ".*", StringComparison.Ordinal) + "$";
        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    [GeneratedRegex(@"^(bin|bin32|bin64|binaries|x64|x86|win32|win64|system|game|application|app)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BinStyleFolderPattern();

    [GeneratedRegex(@"^(v|app-)?\d+(\.\d+)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionFolderPattern();
}

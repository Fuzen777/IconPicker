using System.Diagnostics;
using System.Security;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;
using IconPicker.Core.Rules;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Candidates;

/// <summary>Finds and ranks every icon a folder could take from the files inside it.</summary>
public static class CandidateFinder
{
    /// <summary>Only the best files by name, type and depth are opened; game asset folders can hold thousands of images.</summary>
    public const int InspectedFileLimit = 500;

    /// <summary>
    /// A vector is sharp at any size, so it reports its box scaled to this many pixels on its longest side: a square SVG ranks with the sharpest square pictures.
    /// </summary>
    public const int VectorSize = 1024;

    /// <summary>The vectors among this many first candidates (the picker's first page) are drawn once, to drop those that show nothing.</summary>
    public const int CheckedVectorCount = 50;

    /// <summary>Apps hold hundreds of interface SVGs (VS Code, GIMP, Logi); only the best this many by name and depth are opened, so they don't slow the search.</summary>
    public const int InspectedVectorLimit = 100;

    private const int MaximumIconsPerExe = 20;
    private const int MinimumImageSize = 16;

    /// <summary>The folder's candidates, best first.</summary>
    /// <param name="folder">The folder to search, with everything below it.</param>
    /// <param name="folderNames">The names a file named like the folder may carry.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    public static Task<List<IconCandidate>> FindAsync(DirectoryInfo folder, IReadOnlyList<string> folderNames, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var listedFiles = new List<ListedFile>();
            ListFiles(folder, 0, listedFiles, folderNames, cancellationToken);
            var filesToInspect = LimitVectors(CandidateRanker
                .OrderByListingDetails(listedFiles, file => file.File.FullName, file => file.Kind, file => file.Depth, file => file.File.Length, folderNames))
                .Take(InspectedFileLimit);
            var candidates = new List<IconCandidate>();
            foreach (var listedFile in filesToInspect)
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.AddRange(listedFile.IsShortcut
                    ? await InspectShortcutAsync(listedFile, folder).ConfigureAwait(false)
                    : await InspectAsync(listedFile, folder).ConfigureAwait(false));
            }
            return RemoveEmptyVectors(RemoveRepeatedShortcuts(CandidateRanker.Rank(candidates, folderNames)), cancellationToken);
        }, cancellationToken);
    }

    private static IEnumerable<ListedFile> LimitVectors(IEnumerable<ListedFile> orderedFiles)
    {
        var vectorCount = 0;
        foreach (var listedFile in orderedFiles)
        {
            if (listedFile.Kind != IconSourceKind.Vector || ++vectorCount <= InspectedVectorLimit)
            {
                yield return listedFile;
            }
        }
    }

    /// <summary>
    /// An SVG's size, read from its first element, doesn't tell whether it draws anything (sprite sheets, SVG fonts, templates waiting for a color), so the vectors among the first <see cref="CheckedVectorCount"/> candidates are drawn once and dropped when nothing shows. Further down they stay unchecked: drawing every SVG of a folder holding thousands would slow the search, and applying one that shows nothing fails with a reason.
    /// </summary>
    internal static List<IconCandidate> RemoveEmptyVectors(List<IconCandidate> rankedCandidates, CancellationToken cancellationToken)
    {
        var keptCandidates = new List<IconCandidate>(rankedCandidates.Count);
        foreach (var candidate in rankedCandidates)
        {
            if (keptCandidates.Count < CheckedVectorCount && candidate.Source.Kind == IconSourceKind.Vector)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SvgFile.TryLoad(candidate.Source.FilePath) is not { } svg || !SvgRenderer.HasVisiblePixels(svg))
                {
                    continue;
                }
            }
            keptCandidates.Add(candidate);
        }
        return keptCandidates;
    }

    /// <summary>
    /// A shortcut to an icon the search found anyway only repeats its tile, so the file's own tile is kept. Among shortcuts showing the same icon, the best ranked is kept.
    /// </summary>
    internal static List<IconCandidate> RemoveRepeatedShortcuts(List<IconCandidate> rankedCandidates)
    {
        static string GetIconKey(IconCandidate candidate) => $"{candidate.Source.FilePath},{candidate.Source.IconIndex}";
        var fileIconKeys = rankedCandidates.Where(candidate => !candidate.IsShortcut).Select(GetIconKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shortcutIconKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return rankedCandidates
            .Where(candidate => !candidate.IsShortcut || (!fileIconKeys.Contains(GetIconKey(candidate)) && shortcutIconKeys.Add(GetIconKey(candidate))))
            .ToList();
    }

    /// <summary>The candidates one file offers: each icon of an executable, the icon a shortcut shows, or the file itself. Empty when it can't be read.</summary>
    public static async Task<List<IconCandidate>> InspectFileAsync(string filePath, DirectoryInfo folder)
    {
        var isShortcut = ShortcutReader.IsShortcut(filePath);
        var kind = isShortcut ? IconSourceKind.Module : IconSource.GetKind(filePath);
        if (kind is null || !File.Exists(filePath))
        {
            return [];
        }
        var file = new FileInfo(filePath);
        var depth = FileHelpers.IsInsideFolder(file.FullName, folder.FullName) ? Path.GetRelativePath(folder.FullName, file.DirectoryName!).Split(Path.DirectorySeparatorChar).Count(part => part != ".") : 0;
        var listedFile = new ListedFile(file, kind.Value, depth, isShortcut);
        // A shortcut (dropped on the picker) offers the icon it shows, as it does in a folder's search.
        return isShortcut
            ? await InspectShortcutAsync(listedFile, folder).ConfigureAwait(false)
            : await InspectAsync(listedFile, folder, maximumIconsPerModule: int.MaxValue).ConfigureAwait(false);
    }

    private static void ListFiles(DirectoryInfo folder, int depth, List<ListedFile> listedFiles, IReadOnlyList<string> folderNames, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileInfo[] files;
        try
        {
            files = folder.GetFiles();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            files = [];
        }
        foreach (var file in files)
        {
            if (ShortcutReader.IsShortcut(file.Name))
            {
                if (IsCandidateShortcut(file))
                {
                    // Listed as an executable, the kind most shortcuts lead to; what it really shows is only known once it is read.
                    listedFiles.Add(new ListedFile(file, IconSourceKind.Module, depth, IsShortcut: true));
                }
                continue;
            }
            var kind = IconSource.GetKind(file.Name);
            if (kind is null || !IsCandidateFile(file, kind.Value, folderNames))
            {
                continue;
            }
            listedFiles.Add(new ListedFile(file, kind.Value, depth));
        }
        foreach (var subFolder in FolderSearchRules.GetSubFolders(folder))
        {
            if (!FolderSearchRules.IsSkippedCandidateFolder(subFolder))
            {
                ListFiles(subFolder, depth + 1, listedFiles, folderNames, cancellationToken);
            }
        }
    }

    private static bool IsCandidateFile(FileInfo file, IconSourceKind kind, IReadOnlyList<string> folderNames)
    {
        var attributes = file.Attributes;
        // Cloud-only files would be downloaded just to be looked at. Hidden system files are desktop.ini's world, including the folder's own folder-icon.ico.
        if (FolderSearchRules.IsCloudPlaceholder(attributes) || (attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.System)))
        {
            return false;
        }
        if (kind == IconSourceKind.Vector)
        {
            return file.Length <= SvgFile.MaximumSearchedFileSize;
        }
        if (kind == IconSourceKind.Module)
        {
            if (FolderSearchRules.IsIgnoredExe(file.Name))
            {
                return false;
            }
            // Libraries hold icons too, but a game folder has hundreds of them. Only one named exactly like the folder is searched: apps started through dotnet keep their icon there (tModLoader.dll). Others can still be picked with Browse.
            return string.Equals(file.Extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(file.Extension, ".dll", StringComparison.OrdinalIgnoreCase) && CandidateRanker.GetNameRank(file.FullName, folderNames, trustSizeMarker: false) == 0);
        }
        return true;
    }

    /// <summary>Shortcuts to uninstallers, setups and other helpers are left out, like the helpers themselves.</summary>
    private static bool IsCandidateShortcut(FileInfo shortcut)
    {
        var attributes = shortcut.Attributes;
        return !FolderSearchRules.IsCloudPlaceholder(attributes)
            && !(attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.System))
            && !FolderSearchRules.IsIgnoredExe(shortcut.Name);
    }

    /// <summary>The icon a shortcut shows, as a candidate of its own: named, placed and ranked as the shortcut, drawn and written from the file its icon comes from.</summary>
    private static async Task<List<IconCandidate>> InspectShortcutAsync(ListedFile listedFile, DirectoryInfo folder)
    {
        var shortcut = listedFile.File;
        if (ShortcutReader.ReadIcon(shortcut.FullName) is not { } shortcutIcon)
        {
            return [];
        }
        var iconFile = new FileInfo(shortcutIcon.FilePath);
        // IconResource can't point at a picture, so any other file is read as an .exe or .dll (including .icl icon libraries).
        var kind = IconSource.GetKind(iconFile.Name) ?? IconSourceKind.Module;
        if (FolderSearchRules.IsCloudPlaceholder(iconFile.Attributes) || (kind == IconSourceKind.Module && FolderSearchRules.IsIgnoredExe(iconFile.Name)))
        {
            return [];
        }
        var source = new IconSource(iconFile.FullName, kind, kind == IconSourceKind.Module ? shortcutIcon.IconIndex : 0);
        var size = kind == IconSourceKind.Module ? ReadModuleIconSize(source) : await ReadFileSizeAsync(iconFile.FullName, kind).ConfigureAwait(false);
        if (size is null)
        {
            return [];
        }
        var relativePath = FileHelpers.GetPathRelativeToFolder(shortcut.FullName, folder.FullName);
        var productName = kind == IconSourceKind.Module ? ReadProductName(iconFile.FullName) : null;
        return [new IconCandidate(source, relativePath, listedFile.Depth, iconFile.Length, size.Value.Width, size.Value.Height, productName, shortcut.FullName)];
    }

    /// <summary>The largest size of one icon of an executable, or null when it is missing, damaged or a placeholder.</summary>
    private static (int Width, int Height)? ReadModuleIconSize(IconSource source)
    {
        var iconFile = ExeIconExtractor.ExtractIconFile(source.FilePath, source.IconIndex);
        var sizes = iconFile is null ? null : IcoFileBuilder.ReadImageSizes(iconFile);
        if (sizes is null)
        {
            return null;
        }
        var largestSize = sizes.Max(size => Math.Max(size.Width, size.Height));
        return largestSize >= MinimumImageSize ? (largestSize, largestSize) : null;
    }

    /// <summary>The size of an .ico file (its largest image), a picture or an SVG (scaled to <see cref="VectorSize"/>), or null when it can't be read, or the picture is too small to be an icon.</summary>
    private static async Task<(int Width, int Height)?> ReadFileSizeAsync(string filePath, IconSourceKind kind)
    {
        if (kind == IconSourceKind.IconFile)
        {
            var iconSizes = IcoFileBuilder.ReadImageSizes(filePath);
            return iconSizes is null ? null : (iconSizes.Max(size => size.Width), iconSizes.Max(size => size.Height));
        }
        if (kind == IconSourceKind.Vector)
        {
            if (SvgFile.ReadBox(filePath) is not { } box)
            {
                return null;
            }
            var longestSide = Math.Max(box.Width, box.Height);
            return (Math.Max(1, (int)Math.Round(box.Width / longestSide * VectorSize)), Math.Max(1, (int)Math.Round(box.Height / longestSide * VectorSize)));
        }
        var imageSize = await ImageInspector.TryReadSizeAsync(filePath).ConfigureAwait(false);
        return imageSize is null || Math.Min(imageSize.Value.Width, imageSize.Value.Height) < MinimumImageSize ? null : (imageSize.Value.Width, imageSize.Value.Height);
    }

    private static async Task<List<IconCandidate>> InspectAsync(ListedFile listedFile, DirectoryInfo folder, int maximumIconsPerModule = MaximumIconsPerExe)
    {
        var file = listedFile.File;
        var relativePath = FileHelpers.GetPathRelativeToFolder(file.FullName, folder.FullName);
        switch (listedFile.Kind)
        {
            case IconSourceKind.Module:
                var productName = ReadProductName(file.FullName);
                // Some executables carry 1 px placeholder icons, which would only show as empty tiles.
                return ExeIconExtractor.ListIcons(file.FullName, maximumIconsPerModule)
                    .Where(icon => icon.LargestSize >= MinimumImageSize)
                    .Select(icon => new IconCandidate(new IconSource(file.FullName, IconSourceKind.Module, icon.Index), relativePath, listedFile.Depth, file.Length, icon.LargestSize, icon.LargestSize, productName))
                    .ToList();
            default:
                var size = await ReadFileSizeAsync(file.FullName, listedFile.Kind).ConfigureAwait(false);
                if (size is null)
                {
                    return [];
                }
                return [new IconCandidate(new IconSource(file.FullName, listedFile.Kind), relativePath, listedFile.Depth, file.Length, size.Value.Width, size.Value.Height)];
        }
    }

    /// <summary>The product name in an executable's version information, or null when it has none.</summary>
    private static string? ReadProductName(string modulePath)
    {
        try
        {
            var productName = FileVersionInfo.GetVersionInfo(modulePath).ProductName;
            return string.IsNullOrWhiteSpace(productName) ? null : productName.Trim();
        }
        catch (Exception exception) when (exception is FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed record ListedFile(FileInfo File, IconSourceKind Kind, int Depth, bool IsShortcut = false);
}

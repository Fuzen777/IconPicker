using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;
using IconPicker.Core.Rules;

namespace IconPicker.Core.Applying;

/// <summary>What desktop.ini points at when the icon comes from a file already inside the folder.</summary>
public enum IconTarget
{
    /// <summary>A copy written as folder-icon.ico, which survives updates that rename or move the source.</summary>
    Copy,

    /// <summary>A relative link to the source file itself, with no extra file.</summary>
    Link,
}

public enum IconSourceProblem
{
    /// <summary>The executable has no readable icon at that index.</summary>
    NoIcon,

    /// <summary>The .ico file is damaged.</summary>
    InvalidIconFile,

    /// <summary>The picture can't be decoded or is too large.</summary>
    UnreadableImage,

    /// <summary>The SVG can't be read, or draws nothing visible.</summary>
    UnreadableVector,
}

/// <summary>The chosen file can't give an icon.</summary>
public sealed class IconSourceException(IconSourceProblem problem, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public IconSourceProblem Problem { get; } = problem;
}

/// <summary>Turns a chosen icon and the Icon target setting into what gets written.</summary>
public static class IconTargetResolver
{
    /// <summary>
    /// Whether the icon is linked rather than copied. Pictures and SVGs are always copied, since they have to be converted, and so are files outside the folder, so every icon stays relative and survives the folder moving.
    /// So are icons without a 256 px image: a copy gets enlarged ones (see <see cref="SmallIconEnlarger"/>), where a link would leave Explorer drawing them small.
    /// </summary>
    public static bool WillLink(DirectoryInfo folder, IconSource source, IconTarget target)
    {
        return target == IconTarget.Link
            && !source.IsConverted
            && !source.IsBuilt
            && FileHelpers.IsInsideFolder(source.FilePath, folder.FullName)
            && !IsFolderOwnCopy(folder, source)
            && HasFullSizeImage(source);
    }

    /// <summary>A link into a version folder (app-1.2.3) breaks at the app's next update.</summary>
    public static bool LinksIntoVersionFolder(DirectoryInfo folder, IconSource source, IconTarget target)
    {
        if (!WillLink(folder, source, target))
        {
            return false;
        }
        var relativeFolder = Path.GetRelativePath(folder.FullName, Path.GetDirectoryName(source.FilePath)!);
        return relativeFolder.Split(Path.DirectorySeparatorChar).Any(FolderSearchRules.IsVersionFolderName);
    }

    /// <exception cref="IconSourceException">The file can't give an icon.</exception>
    public static async Task<IconWritePlan> ResolveAsync(DirectoryInfo folder, IconSource source, IconTarget target, CancellationToken cancellationToken)
    {
        if (WillLink(folder, source, target))
        {
            return IconWritePlan.Link(Path.GetRelativePath(folder.FullName, source.FilePath), source.IconIndex);
        }
        switch (source.Kind)
        {
            case IconSourceKind.Module:
                var extractedIcon = ExeIconExtractor.ExtractIconFile(source.FilePath, source.IconIndex)
                    ?? throw new IconSourceException(IconSourceProblem.NoIcon, $"{source.FilePath} has no readable icon at index {source.IconIndex}.");
                return IconWritePlan.Copy(await SmallIconEnlarger.EnlargeAsync(extractedIcon).ConfigureAwait(false));
            case IconSourceKind.IconFile:
                var iconFile = await File.ReadAllBytesAsync(source.FilePath, cancellationToken).ConfigureAwait(false);
                if (IcoFileBuilder.ReadImageSizes(iconFile) is null)
                {
                    throw new IconSourceException(IconSourceProblem.InvalidIconFile, $"{source.FilePath} is not a valid .ico file.");
                }
                return IconWritePlan.Copy(await SmallIconEnlarger.EnlargeAsync(iconFile).ConfigureAwait(false));
            case IconSourceKind.Vector:
                try
                {
                    return IconWritePlan.Copy(await VectorIconConverter.ConvertAsync(source.FilePath, cancellationToken).ConfigureAwait(false));
                }
                catch (InvalidDataException exception)
                {
                    throw new IconSourceException(IconSourceProblem.UnreadableVector, $"{source.FilePath} can't be drawn as an icon.", exception);
                }
            default:
                try
                {
                    return IconWritePlan.Copy(await ImageIconConverter.ConvertAsync(source.FilePath, cancellationToken).ConfigureAwait(false));
                }
                catch (Exception exception) when (exception is InvalidDataException or System.Runtime.InteropServices.COMException or ArgumentException)
                {
                    throw new IconSourceException(IconSourceProblem.UnreadableImage, $"{source.FilePath} can't be converted to an icon.", exception);
                }
        }
    }

    /// <summary>Whether an .exe, .dll or .ico source holds a 256 px image, the size Explorer's largest views draw.</summary>
    private static bool HasFullSizeImage(IconSource source)
    {
        var sizes = source.Kind == IconSourceKind.Module
            ? ExeIconExtractor.ExtractIconFile(source.FilePath, source.IconIndex) is { } iconFile ? IcoFileBuilder.ReadImageSizes(iconFile) : null
            : IcoFileBuilder.ReadImageSizes(source.FilePath);
        return sizes is not null && sizes.Any(size => Math.Max(size.Width, size.Height) >= SmallIconEnlarger.FullSize);
    }

    /// <summary>Linking to the folder's own folder-icon.ico would delete it, since a link replaces the copy.</summary>
    private static bool IsFolderOwnCopy(DirectoryInfo folder, IconSource source)
    {
        return string.Equals(Path.GetFullPath(source.FilePath), Path.Combine(folder.FullName, FolderIconWriter.IconFileName), StringComparison.OrdinalIgnoreCase);
    }
}

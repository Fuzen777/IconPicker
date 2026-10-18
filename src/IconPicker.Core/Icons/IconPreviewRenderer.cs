using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace IconPicker.Core.Icons;

/// <summary>How an icon is fitted into the size asked for.</summary>
public enum IconFit
{
    /// <summary>Within the size and never enlarged, from an icon's largest image: previews and thumbnails.</summary>
    ShrinkOnly,

    /// <summary>Filling the size, enlarged when smaller, from an icon's own image of at least that size, so its hand-drawn small images are used for small sizes: built icons.</summary>
    Fill,
}

/// <summary>Draws icons and pictures as bitmaps the UI can show.</summary>
public static class IconPreviewRenderer
{
    private static readonly Lazy<byte[]?> DefaultFolderIcon = new(() =>
        ExeIconExtractor.ExtractIconFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "imageres.dll"), -3));

    /// <summary>
    /// The icon fitted within size × size pixels, keeping its proportions, as premultiplied BGRA. An SVG always fills a size × size square, centered. Null when the file can't be read.
    /// </summary>
    public static async Task<SoftwareBitmap?> RenderAsync(IconSource source, int size, IconFit fit = IconFit.ShrinkOnly)
    {
        try
        {
            switch (source.Kind)
            {
                case IconSourceKind.Image:
                    using (var fileStream = File.OpenRead(source.FilePath))
                    using (var randomAccessStream = fileStream.AsRandomAccessStream())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                        return await RenderFrameAsync(decoder, size, ExifOrientationMode.RespectExifOrientation, fit);
                    }
                case IconSourceKind.IconFile:
                    return await RenderIconFileAsync(await File.ReadAllBytesAsync(source.FilePath), size, fit);
                case IconSourceKind.Vector:
                    return SvgFile.TryLoad(source.FilePath) is { } svg && SvgRenderer.Render(svg, size).Pixels is { } pixels ? ToSoftwareBitmap(pixels, size) : null;
                default:
                    var iconFile = ExeIconExtractor.ExtractIconFile(source.FilePath, source.IconIndex);
                    return iconFile is null ? null : await RenderIconFileAsync(iconFile, size, fit);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The icon a folder shows: its custom icon, or the standard folder icon when it has none or its icon is broken.</summary>
    public static async Task<SoftwareBitmap?> RenderFolderIconAsync(FolderIconState state, int size)
    {
        if (state is { HasCustomIcon: true, IsBroken: false, IconPath: not null })
        {
            // IconResource can't point at a picture or an SVG; any other file is read as an .exe or .dll (including .icl icon libraries).
            var customSource = new IconSource(state.IconPath, IconSource.GetKind(state.IconPath) ?? IconSourceKind.Module, state.IconIndex);
            if (!customSource.IsConverted)
            {
                var customIcon = await RenderAsync(customSource, size);
                if (customIcon is not null)
                {
                    return customIcon;
                }
            }
        }
        return await RenderStandardFolderIconAsync(size);
    }

    /// <summary>The icon Windows shows for folders without one of their own.</summary>
    public static async Task<SoftwareBitmap?> RenderStandardFolderIconAsync(int size, IconFit fit = IconFit.ShrinkOnly)
    {
        return DefaultFolderIcon.Value is { } defaultIcon ? await RenderIconFileAsync(defaultIcon, size, fit) : null;
    }

    /// <summary>
    /// An image of an .ico file fitted within size × size pixels: its largest, or with <see cref="IconFit.Fill"/> the smallest of at least that size (its largest when none is).
    /// </summary>
    public static async Task<SoftwareBitmap?> RenderIconFileAsync(byte[] iconFileBytes, int size, IconFit fit = IconFit.ShrinkOnly)
    {
        try
        {
            using var iconStream = new InMemoryRandomAccessStream();
            await iconStream.WriteAsync(iconFileBytes.AsBuffer());
            iconStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.IcoDecoderId, iconStream);
            BitmapFrame? largestFrame = null;
            BitmapFrame? smallestLargeEnoughFrame = null;
            for (uint frameIndex = 0; frameIndex < decoder.FrameCount; frameIndex++)
            {
                var frame = await decoder.GetFrameAsync(frameIndex);
                if (largestFrame is null || frame.PixelWidth > largestFrame.PixelWidth)
                {
                    largestFrame = frame;
                }
                if (frame.PixelWidth >= size && (smallestLargeEnoughFrame is null || frame.PixelWidth < smallestLargeEnoughFrame.PixelWidth))
                {
                    smallestLargeEnoughFrame = frame;
                }
            }
            var chosenFrame = fit == IconFit.Fill ? smallestLargeEnoughFrame ?? largestFrame : largestFrame;
            return chosenFrame is null ? null : await RenderFrameAsync(chosenFrame, size, ExifOrientationMode.IgnoreExifOrientation, fit);
        }
        catch (Exception exception) when (exception is COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>A bitmap's premultiplied BGRA pixels centered in a size × size square, with transparent padding when it is smaller or not square.</summary>
    public static byte[] ToSquarePixels(SoftwareBitmap bitmap, int size)
    {
        var bitmapPixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        using (var premultiplied = bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied ? null : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
        {
            (premultiplied ?? bitmap).CopyToBuffer(bitmapPixels.AsBuffer());
        }
        var pixels = new byte[size * size * 4];
        var width = Math.Min(size, bitmap.PixelWidth);
        var height = Math.Min(size, bitmap.PixelHeight);
        var offsetX = (size - width) / 2;
        var offsetY = (size - height) / 2;
        for (var row = 0; row < height; row++)
        {
            System.Buffer.BlockCopy(bitmapPixels, row * bitmap.PixelWidth * 4, pixels, ((offsetY + row) * size + offsetX) * 4, width * 4);
        }
        return pixels;
    }

    /// <summary>size × size premultiplied BGRA pixels as a bitmap the UI can show.</summary>
    public static SoftwareBitmap ToSoftwareBitmap(byte[] premultipliedPixels, int size)
    {
        return SoftwareBitmap.CreateCopyFromBuffer(premultipliedPixels.AsBuffer(), BitmapPixelFormat.Bgra8, size, size, BitmapAlphaMode.Premultiplied);
    }

    private static async Task<SoftwareBitmap> RenderFrameAsync(IBitmapFrameWithSoftwareBitmap frame, int size, ExifOrientationMode orientationMode, IconFit fit)
    {
        var bitmapFrame = (IBitmapFrame)frame;
        var scale = (double)size / Math.Max(bitmapFrame.PixelWidth, bitmapFrame.PixelHeight);
        if (fit == IconFit.ShrinkOnly)
        {
            scale = Math.Min(1.0, scale);
        }
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Round(bitmapFrame.PixelWidth * scale)),
            ScaledHeight = (uint)Math.Max(1, Math.Round(bitmapFrame.PixelHeight * scale)),
            // Fant is best for shrinking; it blocks up enlarged pixels, which cubic smooths.
            InterpolationMode = scale > 1 ? BitmapInterpolationMode.Cubic : BitmapInterpolationMode.Fant,
        };
        return await frame.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, orientationMode, ColorManagementMode.ColorManageToSRgb);
    }
}

namespace IconPicker.Core.Icons;

/// <summary>
/// Gives an icon whose largest image is under 256 px larger images, up to 256 px. Explorer never enlarges a folder icon: in its larger views, a 32 or 48 px icon
/// sits small in the middle of the space. Pixel art is enlarged by whole multiples with hard edges, so it stays crisp; anything else is enlarged smoothly.
/// </summary>
public static class SmallIconEnlarger
{
    /// <summary>The size Explorer's largest views draw a folder icon at.</summary>
    public const int FullSize = 256;

    /// <summary>Above this, an icon is never taken for pixel art: hand-placed pixels are a small-icon craft.</summary>
    private const int PixelArtMaximumSize = 64;

    /// <summary>Pixel art's edges are hard: few of its visible pixels are partly transparent.</summary>
    private const double PixelArtMaximumSoftEdgeShare = 0.1;

    /// <summary>Pixel art uses a small palette, where a smoothly drawn icon blends hundreds of colors.</summary>
    private const int PixelArtMaximumColors = 96;

    /// <summary>The .ico file with images added at every size of <see cref="ImageIconConverter.IconSizes"/> above its largest, or as it is when it already has a 256 px image (or can't be read).</summary>
    public static async Task<byte[]> EnlargeAsync(byte[] iconFile)
    {
        var images = IcoFileBuilder.ReadImages(iconFile);
        if (images is null || images.Count == 0)
        {
            return iconFile;
        }
        var largestSize = images.Max(image => Math.Max(image.Width, image.Height));
        if (largestSize >= FullSize)
        {
            return iconFile;
        }
        // Icons often hold the same size in 16 or 256 colors too: enlarging starts from the richest one.
        var bestImage = images.Where(image => Math.Max(image.Width, image.Height) == largestSize).OrderByDescending(ColorDepth).ThenByDescending(image => image.Data.Length).First();
        var bestIcon = IcoFileBuilder.Build([bestImage]);
        using var largestImage = await IconPreviewRenderer.RenderIconFileAsync(bestIcon, largestSize).ConfigureAwait(false);
        if (largestImage is null)
        {
            return iconFile;
        }
        var largestPixels = IconPreviewRenderer.ToSquarePixels(largestImage, largestSize);
        var isPixelArt = IsPixelArt(largestPixels, largestSize);
        var addedImages = new List<IcoImage>();
        foreach (var size in ImageIconConverter.IconSizes.Where(size => size > largestSize))
        {
            byte[] pixels;
            if (isPixelArt)
            {
                pixels = EnlargeByWholeMultiple(largestPixels, largestSize, size);
            }
            else
            {
                using var enlargedImage = await IconPreviewRenderer.RenderIconFileAsync(bestIcon, size, IconFit.Fill).ConfigureAwait(false);
                if (enlargedImage is null)
                {
                    return iconFile;
                }
                pixels = IconPreviewRenderer.ToSquarePixels(enlargedImage, size);
            }
            addedImages.Add(IcoImage.FromPng(size, SkiaPixels.EncodePng(pixels, size)));
        }
        // Largest first, as converted icons are.
        return IcoFileBuilder.Build([.. addedImages, .. images]);
    }

    /// <summary>Whether size × size premultiplied BGRA pixels look like pixel art: small, with hard edges and a small palette.</summary>
    public static bool IsPixelArt(byte[] premultipliedPixels, int size)
    {
        if (size > PixelArtMaximumSize)
        {
            return false;
        }
        var visibleCount = 0;
        var softEdgeCount = 0;
        var colors = new HashSet<int>();
        for (var offset = 0; offset < premultipliedPixels.Length; offset += 4)
        {
            var alpha = premultipliedPixels[offset + 3];
            if (alpha == 0)
            {
                continue;
            }
            visibleCount++;
            if (alpha < 255)
            {
                softEdgeCount++;
                continue;
            }
            colors.Add(premultipliedPixels[offset] | (premultipliedPixels[offset + 1] << 8) | (premultipliedPixels[offset + 2] << 16));
        }
        return visibleCount > 0 && softEdgeCount <= visibleCount * PixelArtMaximumSoftEdgeShare && colors.Count <= PixelArtMaximumColors;
    }

    /// <summary>Bits per pixel: PNG images (and entries that leave it unset) count as full color.</summary>
    private static int ColorDepth(IcoImage image)
    {
        var bitCount = image.EntryHeader[6];
        var isPng = image.Data.Length > 4 && image.Data[0] == 0x89 && image.Data[1] == (byte)'P';
        return isPng || bitCount == 0 ? 32 : bitCount;
    }

    /// <summary>Each pixel becomes a block of whole pixels, as large as fits in the target size, centered with transparent padding.</summary>
    private static byte[] EnlargeByWholeMultiple(byte[] sourcePixels, int sourceSize, int targetSize)
    {
        var factor = Math.Max(1, targetSize / sourceSize);
        var scaledSize = sourceSize * factor;
        var offset = (targetSize - scaledSize) / 2;
        var targetPixels = new byte[targetSize * targetSize * 4];
        for (var y = 0; y < scaledSize; y++)
        {
            var sourceRow = y / factor * sourceSize;
            var targetRow = (offset + y) * targetSize;
            for (var x = 0; x < scaledSize; x++)
            {
                System.Buffer.BlockCopy(sourcePixels, (sourceRow + (x / factor)) * 4, targetPixels, (targetRow + offset + x) * 4, 4);
            }
        }
        return targetPixels;
    }
}

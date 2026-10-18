using SkiaSharp;

namespace IconPicker.Core.Icons;

/// <summary>Moves premultiplied BGRA pixels in and out of Skia.</summary>
internal static class SkiaPixels
{
    public static SKImageInfo SquareInfo(int size)
    {
        return new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
    }

    /// <summary>size × size premultiplied BGRA pixels as a PNG file.</summary>
    public static byte[] EncodePng(byte[] premultipliedPixels, int size)
    {
        using var image = SKImage.FromPixelCopy(SquareInfo(size), premultipliedPixels, size * 4);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        return pngData.ToArray();
    }
}

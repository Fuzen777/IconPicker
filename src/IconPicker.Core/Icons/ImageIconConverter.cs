using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace IconPicker.Core.Icons;

/// <summary>Turns a picture into a multi-size .ico file.</summary>
public static class ImageIconConverter
{
    /// <summary>The sizes every converted icon holds, largest first.</summary>
    public static IReadOnlyList<int> IconSizes { get; } = [256, 128, 64, 48, 32, 24, 16];

    private const long MaximumPixelCount = 64_000_000;

    /// <summary>
    /// The picture fitted into a square with transparent padding (never cropped), at every size of <see cref="IconSizes"/>, as the bytes of an .ico file.
    /// </summary>
    /// <exception cref="InvalidDataException">The picture is larger than 64 megapixels.</exception>
    public static async Task<byte[]> ConvertAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var randomAccessStream = fileStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        if ((long)decoder.PixelWidth * decoder.PixelHeight > MaximumPixelCount)
        {
            throw new InvalidDataException("The picture is larger than 64 megapixels.");
        }
        var images = new List<IcoImage>(IconSizes.Count);
        foreach (var size in IconSizes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            images.Add(IcoImage.FromPng(size, await RenderSquarePngAsync(decoder, size)));
        }
        return IcoFileBuilder.Build(images);
    }

    /// <summary>Scales the picture to fit a size × size square and centres it on a transparent background.</summary>
    internal static async Task<byte[]> RenderSquarePngAsync(BitmapDecoder decoder, int size)
    {
        // Scaling happens before the EXIF orientation is applied, so it is set in the stored image's own width and height.
        var scale = (double)size / Math.Max(decoder.PixelWidth, decoder.PixelHeight);
        var scaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale));
        var scaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale));
        var transform = new BitmapTransform
        {
            ScaledWidth = scaledWidth,
            ScaledHeight = scaledHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        var pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        var pixels = pixelData.DetachPixelData();
        // A quarter or three-quarter turn swaps width and height.
        var isTurned = decoder.OrientedPixelWidth != decoder.PixelWidth;
        var (width, height) = isTurned ? ((int)scaledHeight, (int)scaledWidth) : ((int)scaledWidth, (int)scaledHeight);

        var canvas = new byte[size * size * 4];
        var offsetX = (size - width) / 2;
        var offsetY = (size - height) / 2;
        for (var row = 0; row < height; row++)
        {
            System.Buffer.BlockCopy(pixels, row * width * 4, canvas, ((offsetY + row) * size + offsetX) * 4, width * 4);
        }
        return await EncodePngAsync(canvas, (uint)size, (uint)size);
    }

    /// <summary>Straight-alpha BGRA pixels as a PNG file.</summary>
    internal static async Task<byte[]> EncodePngAsync(byte[] bgraPixels, uint width, uint height)
    {
        using var encodedStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, encodedStream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, width, height, 96, 96, bgraPixels);
        await encoder.FlushAsync();
        using var readStream = encodedStream.GetInputStreamAt(0).AsStreamForRead();
        using var pngBytes = new MemoryStream();
        await readStream.CopyToAsync(pngBytes);
        return pngBytes.ToArray();
    }
}

using IconPicker.Core.Icons;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace IconPicker.Core.Tests;

internal static class TestImages
{
    /// <summary>A PNG of the given size filled with one opaque colour.</summary>
    public static Task<byte[]> CreatePngAsync(int width, int height, byte blue = 0, byte green = 0, byte red = 255)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = blue;
            pixels[i + 1] = green;
            pixels[i + 2] = red;
            pixels[i + 3] = 255;
        }
        return ImageIconConverter.EncodePngAsync(pixels, (uint)width, (uint)height);
    }

    /// <summary>A 32-bit BMP of the given size filled with one opaque colour.</summary>
    public static async Task<byte[]> CreateBmpAsync(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill<byte>(pixels, 255);
        using var encodedStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, encodedStream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();
        using var readStream = encodedStream.GetInputStreamAt(0).AsStreamForRead();
        using var bmpBytes = new MemoryStream();
        await readStream.CopyToAsync(bmpBytes);
        return bmpBytes.ToArray();
    }

    /// <summary>The straight-alpha BGRA pixels of a PNG, with its size.</summary>
    public static async Task<(byte[] Pixels, int Width, int Height)> DecodePngAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(pngBytes));
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return (pixelData.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}

using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;

namespace IconPicker.Core.Icons;

/// <summary>Reads picture details without decoding the whole picture.</summary>
public static class ImageInspector
{
    /// <summary>The picture's size as displayed (after its EXIF orientation), or null when Windows can't decode it.</summary>
    public static async Task<(int Width, int Height)?> TryReadSizeAsync(string imagePath)
    {
        try
        {
            using var fileStream = File.OpenRead(imagePath);
            using var randomAccessStream = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            return ((int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException or ArgumentException)
        {
            return null;
        }
    }
}

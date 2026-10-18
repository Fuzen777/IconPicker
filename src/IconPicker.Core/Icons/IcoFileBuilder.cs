namespace IconPicker.Core.Icons;

/// <summary>One image inside an .ico file.</summary>
/// <param name="EntryHeader">The 8 header bytes .ico files share with icon resources: width, height, colour count, reserved, planes and bit count.</param>
/// <param name="Data">The image itself, PNG or bitmap.</param>
public sealed record IcoImage(byte[] EntryHeader, byte[] Data)
{
    public int Width => EntryHeader[0] == 0 ? 256 : EntryHeader[0];

    public int Height => EntryHeader[1] == 0 ? 256 : EntryHeader[1];

    /// <summary>A 32-bit PNG image of the given size (256 px at most).</summary>
    public static IcoImage FromPng(int size, byte[] pngBytes)
    {
        var dimension = (byte)(size >= 256 ? 0 : size);
        byte[] entryHeader = [dimension, dimension, 0, 0, 1, 0, 32, 0];
        return new IcoImage(entryHeader, pngBytes);
    }
}

/// <summary>Writes and reads the .ico file format.</summary>
public static class IcoFileBuilder
{
    private const int FileHeaderSize = 6;
    private const int EntrySize = 16;

    /// <summary>An .ico file holding the given images, in order.</summary>
    public static byte[] Build(IReadOnlyList<IcoImage> images)
    {
        using var iconStream = new MemoryStream();
        using var iconWriter = new BinaryWriter(iconStream);
        iconWriter.Write((ushort)0);
        iconWriter.Write((ushort)1);
        iconWriter.Write((ushort)images.Count);
        var imageOffset = FileHeaderSize + images.Count * EntrySize;
        foreach (var image in images)
        {
            iconWriter.Write(image.EntryHeader, 0, 8);
            iconWriter.Write((uint)image.Data.Length);
            iconWriter.Write((uint)imageOffset);
            imageOffset += image.Data.Length;
        }
        foreach (var image in images)
        {
            iconWriter.Write(image.Data);
        }
        iconWriter.Flush();
        return iconStream.ToArray();
    }

    /// <summary>The sizes an .ico file lists in its header, or null when it isn't a valid .ico file.</summary>
    public static IReadOnlyList<(int Width, int Height)>? ReadImageSizes(ReadOnlySpan<byte> iconBytes)
    {
        if (iconBytes.Length < FileHeaderSize || BitConverter.ToUInt16(iconBytes[..2]) != 0 || BitConverter.ToUInt16(iconBytes[2..4]) != 1)
        {
            return null;
        }
        var entryCount = BitConverter.ToUInt16(iconBytes[4..6]);
        if (entryCount == 0 || iconBytes.Length < FileHeaderSize + entryCount * EntrySize)
        {
            return null;
        }
        var sizes = new List<(int Width, int Height)>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = FileHeaderSize + i * EntrySize;
            var width = iconBytes[entryOffset] == 0 ? 256 : iconBytes[entryOffset];
            var height = iconBytes[entryOffset + 1] == 0 ? 256 : iconBytes[entryOffset + 1];
            sizes.Add((width, height));
        }
        return sizes;
    }

    /// <summary>The sizes listed by an .ico file on disk, reading only its header. Null when it isn't a valid .ico file or can't be read.</summary>
    public static IReadOnlyList<(int Width, int Height)>? ReadImageSizes(string iconFilePath)
    {
        try
        {
            using var iconFile = File.OpenRead(iconFilePath);
            Span<byte> fileHeader = stackalloc byte[FileHeaderSize];
            iconFile.ReadExactly(fileHeader);
            var entryCount = BitConverter.ToUInt16(fileHeader[4..6]);
            var headerBytes = new byte[FileHeaderSize + entryCount * EntrySize];
            fileHeader.CopyTo(headerBytes);
            iconFile.ReadExactly(headerBytes, FileHeaderSize, headerBytes.Length - FileHeaderSize);
            return ReadImageSizes(headerBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

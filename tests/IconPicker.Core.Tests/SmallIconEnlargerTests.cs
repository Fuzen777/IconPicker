using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

/// <summary>Icons under 256 px get larger images, so Explorer's largest views don't show them small.</summary>
[TestClass]
public sealed class SmallIconEnlargerTests
{
    [TestMethod]
    public async Task EnlargeAsync_PixelArt_AddsCrispImagesUpTo256()
    {
        // 16 px, two colors, hard edges: each source pixel becomes a 16 × 16 block of exactly its color.
        var pixels = new byte[16 * 16 * 4];
        for (var index = 0; index < 16 * 16; index++)
        {
            var isRed = (index % 16 + index / 16) % 2 == 0;
            pixels[index * 4] = isRed ? (byte)0 : (byte)255;
            pixels[(index * 4) + 2] = isRed ? (byte)255 : (byte)0;
            pixels[(index * 4) + 3] = 255;
        }
        var icon = IcoFileBuilder.Build([IcoImage.FromPng(16, SkiaPixels.EncodePng(pixels, 16))]);

        var enlarged = await SmallIconEnlarger.EnlargeAsync(icon);

        var sizes = IcoFileBuilder.ReadImageSizes(enlarged)!.Select(size => size.Width).ToArray();
        CollectionAssert.AreEquivalent(new[] { 256, 128, 64, 48, 32, 24, 16 }, sizes);
        var full = await RenderAsync(enlarged, 256);
        Assert.IsTrue(SmallIconEnlarger.IsPixelArt(pixels, 16));
        // The first block is red, the second blue, with no blend between them.
        CollectionAssert.AreEqual(Color(full, 256, 15, 0), Color(full, 256, 0, 0));
        CollectionAssert.AreNotEqual(Color(full, 256, 15, 0), Color(full, 256, 16, 0));
        Assert.AreEqual(2, DistinctOpaqueColors(full));
    }

    [TestMethod]
    public async Task EnlargeAsync_SmoothIcon_AddsSmoothImages()
    {
        // A 48 px gradient: hundreds of colors, so not pixel art.
        var pixels = new byte[48 * 48 * 4];
        for (var index = 0; index < 48 * 48; index++)
        {
            pixels[index * 4] = (byte)(index % 48 * 5);
            pixels[(index * 4) + 1] = (byte)(index / 48 * 5);
            pixels[(index * 4) + 3] = 255;
        }
        var icon = IcoFileBuilder.Build([IcoImage.FromPng(48, SkiaPixels.EncodePng(pixels, 48))]);

        var enlarged = await SmallIconEnlarger.EnlargeAsync(icon);

        Assert.IsFalse(SmallIconEnlarger.IsPixelArt(pixels, 48));
        CollectionAssert.IsSubsetOf(new[] { 256, 128, 64 }, IcoFileBuilder.ReadImageSizes(enlarged)!.Select(size => size.Width).ToArray());
        // Filled edge to edge, and smooth: far more shades than the source's own.
        var full = await RenderAsync(enlarged, 256);
        Assert.AreEqual(255, Color(full, 256, 0, 0)[3]);
        Assert.IsGreaterThan(48 * 48, DistinctOpaqueColors(full));
    }

    [TestMethod]
    public async Task EnlargeAsync_FullSizeIcon_IsLeftAsItIs()
    {
        var icon = IcoFileBuilder.Build([IcoImage.FromPng(256, await TestImages.CreatePngAsync(256, 256)), IcoImage.FromPng(32, await TestImages.CreatePngAsync(32, 32))]);

        CollectionAssert.AreEqual(icon, await SmallIconEnlarger.EnlargeAsync(icon));
    }

    [TestMethod]
    public async Task ResolveAsync_SmallIconWithLinkSetting_IsCopiedEnlarged()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");
        var smallIcon = testFolder.CreateFile(@"Game\small.ico", IcoFileBuilder.Build([IcoImage.FromPng(32, await TestImages.CreatePngAsync(32, 32))]));
        var fullIcon = testFolder.CreateFile(@"Game\full.ico", IcoFileBuilder.Build([IcoImage.FromPng(256, await TestImages.CreatePngAsync(256, 256))]));
        var smallSource = new IconSource(smallIcon.FullName, IconSourceKind.IconFile);

        var plan = await IconTargetResolver.ResolveAsync(game, smallSource, IconTarget.Link, CancellationToken.None);

        Assert.IsFalse(IconTargetResolver.WillLink(game, smallSource, IconTarget.Link));
        Assert.IsTrue(IconTargetResolver.WillLink(game, new IconSource(fullIcon.FullName, IconSourceKind.IconFile), IconTarget.Link));
        Assert.AreEqual(Folders.FolderIconWriter.CopiedIconSetting, plan.IconSetting);
        Assert.Contains(256, IcoFileBuilder.ReadImageSizes(plan.IconFileBytes!)!.Select(size => size.Width));
    }

    private static async Task<byte[]> RenderAsync(byte[] icon, int size)
    {
        using var bitmap = await IconPreviewRenderer.RenderIconFileAsync(icon, size);
        return IconPreviewRenderer.ToSquarePixels(bitmap!, size);
    }

    private static byte[] Color(byte[] pixels, int size, int x, int y)
    {
        return pixels.AsSpan(((y * size) + x) * 4, 4).ToArray();
    }

    private static int DistinctOpaqueColors(byte[] pixels)
    {
        var colors = new HashSet<int>();
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] == 255)
            {
                colors.Add(pixels[offset] | (pixels[offset + 1] << 8) | (pixels[offset + 2] << 16));
            }
        }
        return colors.Count;
    }
}

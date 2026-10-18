using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class IconImageTests
{
    [TestMethod]
    public async Task ConvertAsync_NonSquarePicture_IsPaddedNotCropped()
    {
        using var testFolder = new TestFolder();
        var cover = testFolder.CreateFile("cover.png", await TestImages.CreatePngAsync(300, 150));

        var iconFile = await ImageIconConverter.ConvertAsync(cover.FullName);

        var sizes = IcoFileBuilder.ReadImageSizes(iconFile);
        CollectionAssert.AreEqual(new[] { (256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16) }, sizes!.ToArray());
        // The first image is the 256 px PNG; the picture is scaled to 256 × 128 and centred, so the top quarter is transparent.
        var dataOffset = BitConverter.ToInt32(iconFile, 6 + 12);
        var dataLength = BitConverter.ToInt32(iconFile, 6 + 8);
        var (pixels, width, height) = await TestImages.DecodePngAsync(iconFile[dataOffset..(dataOffset + dataLength)]);
        Assert.AreEqual((256, 256), (width, height));
        Assert.AreEqual(0, pixels[((10 * 256) + 128) * 4 + 3]);
        Assert.AreEqual(255, pixels[((128 * 256) + 128) * 4 + 3]);
        Assert.AreEqual(255, pixels[((128 * 256) + 128) * 4 + 2]);
    }

    [TestMethod]
    public async Task RenderAsync_FitsWithinTheSizeWithoutEnlarging()
    {
        using var testFolder = new TestFolder();
        var cover = testFolder.CreateFile("cover.png", await TestImages.CreatePngAsync(300, 150));
        var small = testFolder.CreateFile("small.png", await TestImages.CreatePngAsync(40, 40));

        var coverPreview = await IconPreviewRenderer.RenderAsync(new IconSource(cover.FullName, IconSourceKind.Image), 96);
        var smallPreview = await IconPreviewRenderer.RenderAsync(new IconSource(small.FullName, IconSourceKind.Image), 96);
        var exePreview = await IconPreviewRenderer.RenderAsync(new IconSource(TestFolder.IconExePath, IconSourceKind.Module), 48);

        Assert.AreEqual((96, 48), (coverPreview!.PixelWidth, coverPreview.PixelHeight));
        Assert.AreEqual((40, 40), (smallPreview!.PixelWidth, smallPreview.PixelHeight));
        Assert.IsNotNull(exePreview);
        Assert.IsLessThanOrEqualTo(48, exePreview.PixelWidth);
    }

    [TestMethod]
    public async Task RenderAsync_UnreadableFile_ReturnsNull()
    {
        using var testFolder = new TestFolder();
        var fake = testFolder.CreateTextFile("fake.png", "not a picture");

        Assert.IsNull(await IconPreviewRenderer.RenderAsync(new IconSource(fake.FullName, IconSourceKind.Image), 96));
    }

    [TestMethod]
    public async Task RenderFolderIconAsync_ShowsTheCustomOrTheStandardIcon()
    {
        using var testFolder = new TestFolder();
        var game = testFolder.CreateFolder("Game");

        var standardIcon = await IconPreviewRenderer.RenderFolderIconAsync(FolderIconState.Read(game), 64);
        FolderIconWriter.SetIcon(game, IconWritePlan.Copy(ExeIconExtractor.ExtractIconFile(TestFolder.IconExePath, 0)!));
        var customIcon = await IconPreviewRenderer.RenderFolderIconAsync(FolderIconState.Read(game), 64);

        Assert.IsNotNull(standardIcon);
        Assert.IsNotNull(customIcon);
    }
}

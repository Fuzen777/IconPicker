using IconPicker.Core.Icons;
using SkiaSharp;

namespace IconPicker.Core.Builder;

/// <summary>A finished built icon.</summary>
/// <param name="IconFile">The .ico file, with every size of <see cref="ImageIconConverter.IconSizes"/>.</param>
/// <param name="PreviewPng">The 256 px image as a PNG, for lists.</param>
public sealed record BuiltIcon(byte[] IconFile, byte[] PreviewPng);

/// <summary>Draws a glyph on its background shape, at every size of an .ico.</summary>
public static class IconComposer
{
    /// <summary>The rounded square's corner radius, as a share of its side: close to Windows' own app tiles.</summary>
    private const float CornerRadiusRatio = 0.22f;

    /// <summary>Shapes keep a thin margin, like Windows' own icons, so neighbouring icons don't touch.</summary>
    private const float ShapeMarginRatio = 0.04f;

    /// <summary>Where the glyph's center sits on the folder: the middle of its front panel.</summary>
    private const float FolderEmblemCenterRatio = 0.58f;

    /// <summary>The icon at size × size as premultiplied BGRA, or null when the glyph can't be drawn.</summary>
    public static async Task<byte[]?> ComposeAsync(IIconGlyph glyph, IconStyle style, int size)
    {
        if (style.IconColor is { } iconColor && glyph is VectorGlyph { CanRecolor: true } vectorGlyph)
        {
            glyph = vectorGlyph.WithColor(iconColor);
        }
        var glyphSize = Math.Max(1, (int)Math.Round(size * style.ClampedScale));
        var glyphPixels = await glyph.RenderAsync(glyphSize).ConfigureAwait(false);
        if (glyphPixels is null)
        {
            return null;
        }
        var folderPixels = style.Shape == IconShape.Folder ? await RenderFolderAsync(size).ConfigureAwait(false) : null;

        using var bitmap = new SKBitmap(SkiaPixels.SquareInfo(size));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            if (folderPixels is not null)
            {
                DrawPixels(canvas, folderPixels, size, 0, 0);
            }
            else if (style.UsesBackgroundColor)
            {
                DrawShape(canvas, style, size);
            }
            var glyphX = (size - glyphSize) / 2;
            var glyphY = style.Shape == IconShape.Folder ? (int)Math.Round(size * FolderEmblemCenterRatio - glyphSize / 2.0) : (size - glyphSize) / 2;
            DrawPixels(canvas, glyphPixels, glyphSize, glyphX, Math.Clamp(glyphY, 0, size - glyphSize));
            canvas.Flush();
        }
        var pixels = new byte[size * size * 4];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>The icon at every size, each drawn afresh so small sizes stay sharp, or null when the glyph can't be drawn.</summary>
    public static async Task<BuiltIcon?> BuildAsync(IIconGlyph glyph, IconStyle style, CancellationToken cancellationToken = default)
    {
        var images = new List<IcoImage>(ImageIconConverter.IconSizes.Count);
        byte[]? previewPng = null;
        foreach (var size in ImageIconConverter.IconSizes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pixels = await ComposeAsync(glyph, style, size).ConfigureAwait(false);
            if (pixels is null)
            {
                return null;
            }
            var png = SkiaPixels.EncodePng(pixels, size);
            images.Add(IcoImage.FromPng(size, png));
            previewPng ??= png;
        }
        return new BuiltIcon(IcoFileBuilder.Build(images), previewPng!);
    }

    private static void DrawShape(SKCanvas canvas, IconStyle style, int size)
    {
        var color = SKColor.TryParse(style.BackgroundColor, out var parsedColor) ? parsedColor : SKColors.Gray;
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        var margin = (float)Math.Round(size * ShapeMarginRatio);
        var bounds = new SKRect(margin, margin, size - margin, size - margin);
        switch (style.Shape)
        {
            case IconShape.Square:
                canvas.DrawRect(bounds, paint);
                break;
            case IconShape.Rounded:
                var radius = bounds.Width * CornerRadiusRatio;
                canvas.DrawRoundRect(bounds, radius, radius, paint);
                break;
            case IconShape.Circle:
                canvas.DrawOval(bounds, paint);
                break;
        }
    }

    private static void DrawPixels(SKCanvas canvas, byte[] premultipliedPixels, int size, int x, int y)
    {
        using var image = SKImage.FromPixelCopy(SkiaPixels.SquareInfo(size), premultipliedPixels, size * 4);
        // Drawn at its own size, so no resampling happens.
        canvas.DrawImage(image, x, y, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
    }

    /// <summary>Windows' folder icon filling size × size, from its own image of that size.</summary>
    private static async Task<byte[]?> RenderFolderAsync(int size)
    {
        using var bitmap = await IconPreviewRenderer.RenderStandardFolderIconAsync(size, IconFit.Fill).ConfigureAwait(false);
        return bitmap is null ? null : IconPreviewRenderer.ToSquarePixels(bitmap, size);
    }
}

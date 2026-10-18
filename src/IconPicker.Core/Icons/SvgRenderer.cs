using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace IconPicker.Core.Icons;

public enum SvgRenderStatus
{
    /// <summary>Something visible was drawn.</summary>
    Drawn,

    /// <summary>The SVG drew nothing visible: a sprite sheet, an SVG font, a template waiting for a color.</summary>
    Empty,

    /// <summary>The renderer couldn't read the SVG.</summary>
    Unreadable,
}

/// <summary>A rendered SVG.</summary>
/// <param name="Pixels">size × size premultiplied BGRA pixels, also when nothing visible was drawn. Null when unreadable.</param>
public sealed record SvgRendering(SvgRenderStatus Status, byte[]? Pixels);

public enum SvgColors
{
    /// <summary>Everything visible has one color, or nothing is visible yet: recoloring can't lose detail.</summary>
    Single,

    /// <summary>Several colors, kept as they are.</summary>
    Several,

    Unreadable,
}

/// <summary>Draws SVGs with Svg.Skia, which reads far more real-world SVGs correctly than Windows' own Direct2D renderer.</summary>
public static class SvgRenderer
{
    /// <summary>A pixel at or above this alpha counts as visible.</summary>
    private const byte VisibleAlpha = 8;

    /// <summary>Only fairly opaque pixels are compared for color: anti-aliased edges carry rounding errors once unpremultiplied.</summary>
    private const byte ComparedAlpha = 64;

    private const int ColorTolerance = 24;
    private const int AnalysisSize = 64;

    private static readonly SvgParameters LoadParameters = new(null, null, null, new SvgDocumentLoadOptions
    {
        ProcessingMode = SvgProcessingMode.SecureStatic,
        ExternalResources = SvgExternalResourcePolicy.SameDocumentAndDataOnly,
    });

    static SvgRenderer()
    {
        // References by id across documents go through library-wide switches that ignore the load options above, so they are turned off as well.
        Svg.SvgDocument.ResolveExternalImages = ExternalType.None;
        Svg.SvgDocument.ResolveExternalElements = ExternalType.None;
        Svg.SvgDocument.ResolveExternalXmlEntites = ExternalType.None;
    }

    /// <summary>The SVG fitted into a size × size square and centered, with transparent padding when it isn't square.</summary>
    public static SvgRendering Render(SvgFile svg, int size)
    {
        try
        {
            // Svg.Skia isn't documented as thread-safe, so every render gets its own SKSvg.
            using var renderer = new SKSvg();
            renderer.Settings.EnableJavaScript = false;
            renderer.Settings.EnableExternalJavaScript = false;
            renderer.Settings.EnableBrokenImagePlaceholders = false;
            using var svgStream = new MemoryStream(svg.ToBytes());
            var picture = renderer.Load(svgStream, LoadParameters);
            if (picture is null)
            {
                return new SvgRendering(SvgRenderStatus.Unreadable, null);
            }
            var pictureBounds = picture.CullRect;
            var pixels = new byte[size * size * 4];
            if (pictureBounds.Width <= 0 || pictureBounds.Height <= 0 || !float.IsFinite(pictureBounds.Width) || !float.IsFinite(pictureBounds.Height))
            {
                return new SvgRendering(SvgRenderStatus.Empty, pixels);
            }
            var scale = Math.Min(size / pictureBounds.Width, size / pictureBounds.Height);
            using (var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.Translate((size - pictureBounds.Width * scale) / 2f, (size - pictureBounds.Height * scale) / 2f);
                    canvas.Scale(scale);
                    canvas.Translate(-pictureBounds.Left, -pictureBounds.Top);
                    // The picture's bounds are only a hint: without the clip, content outside the viewBox spills into the padding.
                    canvas.ClipRect(pictureBounds);
                    canvas.DrawPicture(picture);
                    canvas.Flush();
                }
                System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
            }
            return new SvgRendering(HasVisiblePixel(pixels) ? SvgRenderStatus.Drawn : SvgRenderStatus.Empty, pixels);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new SvgRendering(SvgRenderStatus.Unreadable, null);
        }
    }

    /// <summary>Whether the SVG draws anything visible.</summary>
    public static bool HasVisiblePixels(SvgFile svg)
    {
        return Render(svg, AnalysisSize).Status == SvgRenderStatus.Drawn;
    }

    /// <summary>Whether the SVG is drawn in one color, so recoloring it loses nothing (two-tone icons count as several, and so do masked ones, whose cut-outs recoloring would fill in).</summary>
    public static SvgColors AnalyzeColors(SvgFile svg)
    {
        if (svg.HasMask)
        {
            return SvgColors.Several;
        }
        var rendering = Render(svg, AnalysisSize);
        switch (rendering.Status)
        {
            case SvgRenderStatus.Unreadable:
                return SvgColors.Unreadable;
            case SvgRenderStatus.Empty:
                return SvgColors.Single;
        }
        var pixels = rendering.Pixels!;
        // The most opaque pixel gives the reference color; every other fairly opaque pixel has to match it.
        var referenceOffset = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] > pixels[referenceOffset + 3])
            {
                referenceOffset = offset;
            }
        }
        var (referenceBlue, referenceGreen, referenceRed) = Unpremultiply(pixels, referenceOffset);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] < ComparedAlpha)
            {
                continue;
            }
            var (blue, green, red) = Unpremultiply(pixels, offset);
            if (Math.Abs(blue - referenceBlue) > ColorTolerance || Math.Abs(green - referenceGreen) > ColorTolerance || Math.Abs(red - referenceRed) > ColorTolerance)
            {
                return SvgColors.Several;
            }
        }
        return SvgColors.Single;
    }

    private static bool HasVisiblePixel(byte[] pixels)
    {
        for (var offset = 3; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset] >= VisibleAlpha)
            {
                return true;
            }
        }
        return false;
    }

    private static (int Blue, int Green, int Red) Unpremultiply(byte[] pixels, int offset)
    {
        var alpha = pixels[offset + 3];
        return alpha == 0 ? (0, 0, 0) : (pixels[offset] * 255 / alpha, pixels[offset + 1] * 255 / alpha, pixels[offset + 2] * 255 / alpha);
    }
}

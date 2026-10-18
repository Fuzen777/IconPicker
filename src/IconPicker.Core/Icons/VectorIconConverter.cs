namespace IconPicker.Core.Icons;

/// <summary>Turns an SVG into a multi-size .ico file, drawing every size from the vector so small sizes stay sharp.</summary>
public static class VectorIconConverter
{
    /// <summary>The SVG fitted into a square at every size of <see cref="ImageIconConverter.IconSizes"/>, as the bytes of an .ico file.</summary>
    /// <exception cref="InvalidDataException">The SVG can't be read, or draws nothing visible.</exception>
    public static byte[] Convert(SvgFile svg)
    {
        var images = new List<IcoImage>(ImageIconConverter.IconSizes.Count);
        foreach (var size in ImageIconConverter.IconSizes)
        {
            var rendering = SvgRenderer.Render(svg, size);
            // Thin lines can vanish at the smallest sizes; only the largest has to show something.
            if (rendering.Status == SvgRenderStatus.Unreadable || (images.Count == 0 && rendering.Status == SvgRenderStatus.Empty))
            {
                throw new InvalidDataException(rendering.Status == SvgRenderStatus.Empty ? "The SVG draws nothing visible." : "The SVG can't be rendered.");
            }
            images.Add(IcoImage.FromPng(size, SkiaPixels.EncodePng(rendering.Pixels!, size)));
        }
        return IcoFileBuilder.Build(images);
    }

    /// <inheritdoc cref="Convert(SvgFile)"/>
    /// <exception cref="InvalidDataException">The file isn't a readable SVG, or draws nothing visible.</exception>
    public static Task<byte[]> ConvertAsync(string svgPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var svg = SvgFile.TryLoad(svgPath) ?? throw new InvalidDataException("The file isn't a readable SVG.");
            return Convert(svg);
        }, cancellationToken);
    }
}

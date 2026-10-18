using System.Text;
using IconPicker.Core.Icons;

namespace IconPicker.Core.Tests;

[TestClass]
public sealed class SvgTests
{
    private const int RenderSize = 64;

    [TestMethod]
    public void Render_NonSquareViewBox_IsCenteredWithTransparentPadding()
    {
        var svg = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100"><rect width="200" height="100" fill="#ff0000"/></svg>""");

        var rendering = SvgRenderer.Render(svg, RenderSize);

        Assert.AreEqual(SvgRenderStatus.Drawn, rendering.Status);
        Assert.AreEqual(0, PixelAt(rendering, 32, 4).Alpha);
        Assert.AreEqual((255, 0, 0, 255), PixelAt(rendering, 32, 32));
        Assert.AreEqual(0, PixelAt(rendering, 32, 60).Alpha);
    }

    [TestMethod]
    [DataRow("""<rect width="10" height="10" fill="#ff0000"/>""", DisplayName = "fill attribute")]
    [DataRow("""<rect width="10" height="10" style="fill: red; opacity: 1"/>""", DisplayName = "style attribute")]
    [DataRow("""<style>.shape { fill: #f00 }</style><rect class="shape" width="10" height="10"/>""", DisplayName = "style element")]
    [DataRow("""<rect width="10" height="10" fill="currentColor" color="red"/>""", DisplayName = "currentColor")]
    [DataRow("""<rect width="10" height="10"/>""", DisplayName = "no color")]
    [DataRow("""<rect width="10" height="10" fill="context-fill"/>""", DisplayName = "context-fill template")]
    public void Recolor_ReplacesEverySolidPaint(string content)
    {
        var svg = Parse($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">{content}</svg>""");

        var rendering = SvgRenderer.Render(svg.Recolor("#00ff00"), RenderSize);

        Assert.AreEqual((0, 255, 0, 255), PixelAt(rendering, 32, 32));
    }

    [TestMethod]
    public void Recolor_KeepsNoneAndGradientReferences()
    {
        var svg = Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 10">
              <defs><linearGradient id="g"><stop offset="0" stop-color="#0000ff"/><stop offset="1" stop-color="#0000ff"/></linearGradient></defs>
              <rect width="10" height="10" fill="url(#g)"/>
              <rect x="10" width="10" height="10" fill="none" stroke="none"/>
            </svg>
            """);

        var rendering = SvgRenderer.Render(svg.Recolor("#00ff00"), RenderSize);

        // The gradient's stops are solid paints, so they are recolored; the empty half stays empty.
        Assert.AreEqual((0, 255, 0, 255), PixelAt(rendering, 16, 32));
        Assert.AreEqual(0, PixelAt(rendering, 48, 32).Alpha);
    }

    [TestMethod]
    public async Task Render_LinkedImageAndUse_AreNotLoaded()
    {
        using var testFolder = new TestFolder();
        var other = testFolder.CreateTextFile("other.svg", """<svg xmlns="http://www.w3.org/2000/svg"><rect id="r" width="10" height="10" fill="#ff0000"/></svg>""");
        var picture = testFolder.CreateFile("picture.png", await TestImages.CreatePngAsync(10, 10));
        var svg = Parse($"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10">
              <image href="{new Uri(picture.FullName)}" width="10" height="10"/>
              <use xlink:href="{new Uri(other.FullName)}#r"/>
              <image href="{picture.FullName}" width="10" height="10"/>
            </svg>
            """);

        Assert.AreEqual(SvgRenderStatus.Empty, SvgRenderer.Render(svg, RenderSize).Status);
    }

    [TestMethod]
    public void Render_ClipPathInAnotherFile_IsNotLoaded()
    {
        using var testFolder = new TestFolder();
        var clip = testFolder.CreateTextFile("clip.svg", """<svg xmlns="http://www.w3.org/2000/svg"><clipPath id="c"><rect width="1" height="1"/></clipPath></svg>""");
        var svg = Parse($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="#0000ff" clip-path="url({new Uri(clip.FullName)}#c)"/></svg>""");

        var rendering = SvgRenderer.Render(svg, RenderSize);

        // Loaded, the clip would leave only the top-left corner.
        Assert.AreEqual((0, 0, 255, 255), PixelAt(rendering, 48, 48));
    }

    [TestMethod]
    public void Render_LinkedAndImportedStylesheets_AreNotLoaded()
    {
        using var testFolder = new TestFolder();
        var stylesheet = testFolder.CreateTextFile("red.css", "rect { fill: #ff0000 !important; }");
        var stylesheetUri = new Uri(stylesheet.FullName);
        var svg = Parse($"""
            <?xml-stylesheet type="text/css" href="{stylesheetUri}"?>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <style>@import url("{stylesheetUri}");</style>
              <rect width="10" height="10" fill="#0000ff"/>
            </svg>
            """);

        Assert.AreEqual((0, 0, 255, 255), PixelAt(SvgRenderer.Render(svg, RenderSize), 32, 32));
    }

    [TestMethod]
    public void TryParse_RemovesWebFontsScriptsAndEventHandlers()
    {
        var svg = Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" onload="alert(1)">
              <style>@font-face { font-family: Web; src: url(file:///C:/Windows/Fonts/arial.ttf); } text { font-family: Web; }</style>
              <script>alert(2)</script>
              <rect width="10" height="10" style="fill: url(http://example.com/paint.svg#p)"/>
            </svg>
            """);

        var cleaned = Encoding.UTF8.GetString(svg.ToBytes());

        Assert.DoesNotContain("@font-face", cleaned);
        Assert.DoesNotContain("script", cleaned);
        Assert.DoesNotContain("onload", cleaned);
        Assert.DoesNotContain("example.com", cleaned);
        Assert.DoesNotContain("arial", cleaned);
    }

    [TestMethod]
    public void TryParse_ExpandsDtdEntitiesWithoutLoadingAnything()
    {
        var svg = Parse("""
            <?xml version="1.0"?>
            <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd" [
              <!ENTITY red "#ff0000">
            ]>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="&red;"/></svg>
            """);

        Assert.AreEqual((255, 0, 0, 255), PixelAt(SvgRenderer.Render(svg, RenderSize), 32, 32));
        Assert.DoesNotContain("DOCTYPE", Encoding.UTF8.GetString(svg.ToBytes()));
    }

    [TestMethod]
    public void AnalyzeColors_TellsSingleFromSeveralColors()
    {
        var singleColor = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><circle cx="5" cy="5" r="4" fill="#336699"/><rect width="3" height="3" fill="#336699"/></svg>""");
        var twoTone = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10"/><circle cx="5" cy="5" r="2" fill="#ffffff"/></svg>""");
        var template = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="context-fill"/></svg>""");

        Assert.AreEqual(SvgColors.Single, SvgRenderer.AnalyzeColors(singleColor));
        Assert.AreEqual(SvgColors.Several, SvgRenderer.AnalyzeColors(twoTone));
        Assert.AreEqual(SvgColors.Single, SvgRenderer.AnalyzeColors(template));
    }

    [TestMethod]
    [DataRow("not an svg at all")]
    [DataRow("<html><body/></html>")]
    [DataRow("""<svg xmlns="http://www.w3.org/2000/svg"><rect></svg>""")]
    public void TryParse_UnreadableSvg_ReturnsNull(string content)
    {
        Assert.IsNull(SvgFile.TryParse(Encoding.UTF8.GetBytes(content)));
    }

    [TestMethod]
    public void Render_SpriteSheetOrInvalidValues_NeverThrows()
    {
        var spriteSheet = Parse("""<svg xmlns="http://www.w3.org/2000/svg"><defs><symbol id="a" viewBox="0 0 10 10"><rect width="10" height="10"/></symbol></defs></svg>""");
        var invalidValues = Parse("""<svg xmlns="http://www.w3.org/2000/svg" width="1e999999" height="-5" viewBox="a b c d"><path d="M 0 0 L x y Z q" stroke-width="NaN" transform="rotate(nope)"/></svg>""");

        Assert.AreEqual(SvgRenderStatus.Empty, SvgRenderer.Render(spriteSheet, RenderSize).Status);
        Assert.AreNotEqual(SvgRenderStatus.Drawn, SvgRenderer.Render(invalidValues, RenderSize).Status);
        Assert.IsFalse(SvgRenderer.HasVisiblePixels(spriteSheet));
    }

    [TestMethod]
    public void TryParse_DocumentsTooDeepOrTooLargeToDraw_AreUnreadable()
    {
        // Svg.Skia recurses per level: this deep a document would overflow the stack and end the process.
        var deeplyNested = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">{string.Concat(Enumerable.Repeat("<g>", 2000))}<rect width="10" height="10"/>{string.Concat(Enumerable.Repeat("</g>", 2000))}</svg>""";
        var longChain = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><defs><rect id="a0" width="10" height="10"/>{string.Concat(Enumerable.Range(1, 2000).Select(index => $"<use id=\"a{index}\" href=\"#a{index - 1}\"/>"))}</defs><use href="#a2000"/></svg>""";
        // Ten copies of ten copies…: a few hundred bytes drawing ten million rectangles.
        var expanding = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><defs><rect id="a0" width="1" height="1"/>{string.Concat(Enumerable.Range(1, 7).Select(level => $"<g id=\"a{level}\">{string.Concat(Enumerable.Repeat($"<use href=\"#a{level - 1}\"/>", 10))}</g>"))}</defs><use href="#a7"/></svg>""";
        var loop = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><g id="a"><use href="#b"/></g><g id="b"><use href="#a"/></g></svg>""";
        var reasonable = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">{string.Concat(Enumerable.Repeat("<g>", 100))}<rect id="r" width="10" height="10"/>{string.Concat(Enumerable.Repeat("</g>", 100))}<use href="#r"/><use href="#r"/></svg>""";

        Assert.IsNull(SvgFile.TryParse(Encoding.UTF8.GetBytes(deeplyNested)));
        Assert.IsNull(SvgFile.TryParse(Encoding.UTF8.GetBytes(longChain)));
        Assert.IsNull(SvgFile.TryParse(Encoding.UTF8.GetBytes(expanding)));
        Assert.IsNull(SvgFile.TryParse(Encoding.UTF8.GetBytes(loop)));
        Assert.AreEqual(SvgRenderStatus.Drawn, SvgRenderer.Render(Parse(reasonable), RenderSize).Status);
    }

    [TestMethod]
    public void AnalyzeColors_MaskedSvg_KeepsItsColors()
    {
        var masked = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><mask id="m"><rect width="24" height="24" fill="white"/><circle cx="12" cy="12" r="6" fill="black"/></mask><rect width="24" height="24" fill="#000" mask="url(#m)"/></svg>""");

        Assert.IsTrue(masked.HasMask);
        Assert.AreEqual(SvgColors.Several, SvgRenderer.AnalyzeColors(masked));
    }

    [TestMethod]
    public void ReadBox_ReadsTheViewBoxOrTheSize()
    {
        using var testFolder = new TestFolder();
        var withViewBox = testFolder.CreateTextFile("a.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 48 24"/>""");
        var withSize = testFolder.CreateTextFile("b.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="1in" height="48pt"/>""");
        var withPercent = testFolder.CreateTextFile("c.svg", """<svg xmlns="http://www.w3.org/2000/svg" width="100%"/>""");
        var notSvg = testFolder.CreateTextFile("d.svg", "<html/>");

        Assert.AreEqual(new SvgBox(48, 24), SvgFile.ReadBox(withViewBox.FullName));
        Assert.AreEqual(new SvgBox(96, 64), SvgFile.ReadBox(withSize.FullName));
        Assert.IsNull(SvgFile.ReadBox(withPercent.FullName));
        Assert.IsNull(SvgFile.ReadBox(notSvg.FullName));
        Assert.IsTrue(new SvgBox(100, 97).IsNearlySquare);
        Assert.IsFalse(new SvgBox(48, 24).IsNearlySquare);
    }

    [TestMethod]
    public async Task ConvertAsync_GivesEverySizeAndRefusesEmptySvgs()
    {
        using var testFolder = new TestFolder();
        var logo = testFolder.CreateTextFile("logo.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 20"><rect width="10" height="20" fill="#ff0000"/></svg>""");
        var empty = testFolder.CreateTextFile("empty.svg", """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"/>""");

        var iconFile = await VectorIconConverter.ConvertAsync(logo.FullName);

        CollectionAssert.AreEqual(new[] { (256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16) }, IcoFileBuilder.ReadImageSizes(iconFile)!.ToArray());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => VectorIconConverter.ConvertAsync(empty.FullName));
    }

    private static SvgFile Parse(string content)
    {
        return SvgFile.TryParse(Encoding.UTF8.GetBytes(content.Trim())) ?? throw new AssertFailedException("The test SVG didn't parse.");
    }

    /// <summary>The unpremultiplied color of one pixel, as (red, green, blue, alpha).</summary>
    private static (int Red, int Green, int Blue, int Alpha) PixelAt(SvgRendering rendering, int x, int y)
    {
        var offset = (y * RenderSize + x) * 4;
        var pixels = rendering.Pixels!;
        var alpha = pixels[offset + 3];
        return alpha == 0 ? (0, 0, 0, 0) : (pixels[offset + 2] * 255 / alpha, pixels[offset + 1] * 255 / alpha, pixels[offset] * 255 / alpha, alpha);
    }
}

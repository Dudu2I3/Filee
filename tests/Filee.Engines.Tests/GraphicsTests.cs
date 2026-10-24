// Images and vector graphics: the extra raster formats (JPEG XL, JPEG 2000, PSD, TGA, PPM, XCF, camera RAW,
// EMF/WMF), SVG → PDF/PNG, SVGZ, ICNS, Illustrator files and Ghostscript (EPS/PS, skipped when not installed).

using System.IO.Compression;
using System.Text;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Icns;
using Filee.Engines.Magick;
using Filee.Engines.Vector;
using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Filee.Engines.Tests;

public class GraphicsTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static bool GhostscriptInstalled => GhostscriptConverter.Locate() is not null;

    private static void AssertDone(ConversionJob job)
    {
        var errors = string.Join("; ", job.Files.Where(f => f.State == FileState.Failed).Select(f => $"{f.ErrorKey} {f.ErrorDetail}"));
        Assert.True(job.State == JobState.Completed, $"{job.State}: {errors}");
        Assert.All(job.Outputs, o => Assert.True(new FileInfo(o).Length > 0, o));
    }

    private static IMagickColor<byte> Pixel(IMagickImage<byte> image, int x, int y) =>
        image.GetPixels().GetPixel(x, y).ToColor()!;

    private static string Write(string folder, string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    // ───────────────────────── Raster formats ─────────────────────────

    [Fact]
    public void Camera_raw_uses_the_camera_white_balance_and_srgb()
    {
        var settings = ImageReader.Settings("raw", new Preset());

        Assert.Equal(MagickFormat.Dng, settings.Format);
        Assert.Equal("true", settings.GetDefine(MagickFormat.Dng, "use-camera-wb"));
        Assert.Equal("false", settings.GetDefine(MagickFormat.Dng, "use-auto-wb"));
        Assert.Equal("1", settings.GetDefine(MagickFormat.Dng, "output-color"));
    }

    [Fact]
    public async Task Windows_metafiles_are_rendered_at_the_preset_dpi()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "EMF/WMF are read through GDI+ on Windows only.");
        var dir = fx.NewFolder();
        var emf = Path.Combine(dir, "chart.emf");
        var wmf = Path.Combine(dir, "clip.wmf");
        GraphicsBuilders.Emf(emf, 60, 30);
        GraphicsBuilders.Wmf(wmf, 60, 30);

        var job = await fx.ConvertAsync([emf, wmf], new Preset { TargetFormat = "png", Pdf = { RenderDpi = 200 } });
        var pdf = await fx.ConvertAsync([emf], new Preset { TargetFormat = "pdf" });

        AssertDone(job);
        AssertDone(pdf);
        foreach (var output in job.Outputs)
        {
            using var image = new MagickImage(output);
            // 60 mm at 200 dpi = 472 px (a pixel or two either way from GDI's rounding).
            Assert.InRange((int)image.Width, 460, 485);
            Assert.InRange((int)image.Height, 225, 245);
            var left = Pixel(image, (int)image.Width / 6, (int)image.Height / 2);
            var right = Pixel(image, (int)image.Width * 5 / 6, (int)image.Height / 2);
            Assert.True(left.R > 200 && left.B < 100, Path.GetFileName(output) + " left: " + left);
            Assert.True(right.G > 120 && right.R < 60, Path.GetFileName(output) + " right: " + right);
        }
        using var document = PdfReader.Open(pdf.Outputs.Single(), PdfDocumentOpenMode.Import);
        Assert.InRange(document.Pages[0].Width.Millimeter, 58, 62);
    }

    // ───────────────────────── SVG ─────────────────────────

    [Fact]
    public async Task Svg_to_pdf_is_vector()
    {
        var dir = fx.NewFolder();
        var svg = Write(dir, "drawing.svg", GraphicsBuilders.Svg);

        var job = await fx.ConvertAsync([svg], new Preset { TargetFormat = "pdf" });

        AssertDone(job);
        using var document = PdfReader.Open(job.Outputs.Single(), PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
        var page = document.Pages[0];
        // 400 × 200 CSS px = 300 × 150 pt.
        Assert.Equal(300, page.Width.Point, 1);
        Assert.Equal(150, page.Height.Point, 1);

        var content = new StringBuilder(Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue));
        var images = new List<(int Width, int Height)>();
        var fonts = 0;
        CollectResources(page.Resources, content, images, ref fonts, 0);
        var text = content.ToString();
        Assert.Matches(@"\s(c|l|re)\s", text); // path operators: curves, lines, rectangles
        Assert.Matches(@"\sf\*?\s", text);    // fills
        // Text is drawn as glyph outlines: Skia would embed whole font files (13 MB for one line of Korean).
        Assert.DoesNotContain("BT", text);
        Assert.Equal(0, fonts);
        Assert.True(new FileInfo(job.Outputs.Single()).Length < 100_000, "the PDF should be small");
        Assert.DoesNotContain(images, i => i.Width > 64 || i.Height > 64); // no picture of the drawing
    }

    /// <summary>Collects content of form XObjects, image sizes and font count, recursively.</summary>
    private static void CollectResources(PdfDictionary? resources, StringBuilder content, List<(int, int)> images, ref int fonts, int depth)
    {
        if (resources is null || depth > 5)
            return;
        fonts += resources.Elements.GetDictionary("/Font")?.Elements.Count ?? 0;
        var xobjects = resources.Elements.GetDictionary("/XObject");
        if (xobjects is null)
            return;
        foreach (var key in xobjects.Elements.Keys)
        {
            if (xobjects.Elements.GetDictionary(key) is { } dictionary)
            {
                var subtype = dictionary.Elements.GetName("/Subtype");
                if (subtype == "/Image")
                {
                    images.Add((dictionary.Elements.GetInteger("/Width"), dictionary.Elements.GetInteger("/Height")));
                }
                else if (subtype == "/Form")
                {
                    content.Append(Encoding.Latin1.GetString(dictionary.Stream.UnfilteredValue));
                    CollectResources(dictionary.Elements.GetDictionary("/Resources"), content, images, ref fonts, depth + 1);
                }
            }
        }
    }

    [Fact]
    public async Task Svg_to_png_is_rendered_large_enough_and_keeps_transparency()
    {
        var dir = fx.NewFolder();
        var svg = Write(dir, "drawing.svg", GraphicsBuilders.Svg);

        var job = await fx.ConvertAsync([svg], new Preset { TargetFormat = "png" });
        var sized = await fx.ConvertAsync([svg], new Preset
        {
            TargetFormat = "webp",
            Image = { Resize = ResizeMode.LongEdge, LongEdge = 300, WebpLossless = true },
        });
        var jpg = await fx.ConvertAsync([svg], new Preset { TargetFormat = "jpg" });

        AssertDone(job);
        AssertDone(sized);
        AssertDone(jpg);
        using (var png = new MagickImage(job.Outputs.Single()))
        {
            // At least 1024 px on the long edge (the default 150 dpi would give only 625 px).
            Assert.Equal((1024u, 512u), (png.Width, png.Height));
            Assert.Equal(0, Pixel(png, 3, 3).A);
            var circle = Pixel(png, 512, 256);
            Assert.True(circle.R > 240 && circle.G < 20 && circle.A == 255, circle.ToString());
        }
        using (var webp = new MagickImage(sized.Outputs.Single()))
            Assert.Equal((300u, 150u), (webp.Width, webp.Height)); // rendered at exactly that size
        using (var flat = new MagickImage(jpg.Outputs.Single()))
            Assert.True(Pixel(flat, 3, 3).R > 245, "JPG gets the preset background");
    }

    [Fact]
    public void Svg_external_resources_stay_in_the_drawings_folder()
    {
        var dir = fx.NewFolder();
        var sourceDir = Path.Combine(dir, "source");
        var outsideDir = Path.Combine(dir, "source-outside"); // shares the folder name's prefix
        Directory.CreateDirectory(Path.Combine(sourceDir, "images"));
        Directory.CreateDirectory(outsideDir);
        using (var allowed = new MagickImage(MagickColors.Lime, 20, 20))
            allowed.Write(Path.Combine(sourceDir, "allowed.png"));
        using (var nested = new MagickImage(MagickColors.Blue, 20, 20))
            nested.Write(Path.Combine(sourceDir, "images", "nested.png"));
        var secret = Path.Combine(outsideDir, "secret.png");
        using (var outside = new MagickImage(MagickColors.Red, 20, 20))
            outside.Write(secret);
        Write(outsideDir, "shapes.svg", """
            <svg xmlns="http://www.w3.org/2000/svg"><rect id="r" width="20" height="20" fill="#FF0000"/></svg>
            """);
        var svg = Write(sourceDir, "drawing.svg", $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="20">
              <image href="allowed.png" width="20" height="20"/>
              <image href="images/nested.png" x="20" width="20" height="20"/>
              <image href="../source-outside/secret.png" x="40" width="20" height="20"/>
              <image href="{new Uri(secret).AbsoluteUri}" x="60" width="20" height="20"/>
              <use href="../source-outside/shapes.svg#r" x="80"/>
            </svg>
            """);

        using var drawing = SvgRenderer.Load(svg);
        using var rendered = SvgRenderer.Rasterize(drawing, 1);

        var same = Pixel(rendered, 10, 10);
        Assert.True(same.G > 200 && same.R < 50, "image beside the drawing: " + same);
        var below = Pixel(rendered, 30, 10);
        Assert.True(below.B > 200 && below.R < 50, "image in a subfolder: " + below);
        foreach (var x in new[] { 50, 70, 90 })
        {
            var blocked = Pixel(rendered, x, 10);
            Assert.False(blocked.R > 200 && blocked.G < 50 && blocked.B < 50, $"resource outside the folder was drawn at x={x}: " + blocked);
        }
    }

    [Fact]
    public async Task Svg_with_a_loose_ampersand_and_html_entities_still_converts()
    {
        // Issue #120: generated SVGs with "A & B" in a <text> stopped the XML parser ("parsing EntityName").
        var dir = fx.NewFolder();
        var svg = Write(dir, "gemini.svg", """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <rect width="200" height="100" fill="#ff0000"/>
              <text x="10" y="50" font-size="20">Q&A &amp; R&nbsp;D &copy; 2026 &bogus;</text>
            </svg>
            """);

        var job = await fx.ConvertAsync([svg], new Preset { TargetFormat = "png" });

        AssertDone(job);
        using var png = new MagickImage(job.Outputs.Single());
        Assert.True(Pixel(png, 3, 3).R > 240, "the drawing was rendered");
    }

    [Theory]
    [InlineData("<t>A & B</t>", "<t>A &amp; B</t>")]
    [InlineData("<t>R&nbsp;D &copy;</t>", "<t>R&#160;D &#169;</t>")]
    [InlineData("<t>&bogus; &copy</t>", "<t>&amp;bogus; &amp;copy</t>")]
    [InlineData("<a href=\"x?a=1&b=2\"/>", "<a href=\"x?a=1&amp;b=2\"/>")]
    public void Loose_ampersands_are_escaped_and_html_entities_become_references(string svg, string repaired) =>
        Assert.Equal(repaired, Encoding.UTF8.GetString(SvgXmlRepair.FixAmpersands(Encoding.UTF8.GetBytes(svg))!));

    [Theory]
    [InlineData("<t>A &amp; B &lt; &#38; &#x26;</t>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY ns_svg \"http://www.w3.org/2000/svg\">]><svg xmlns=\"&ns_svg;\"/>")]
    [InlineData("<t><!-- a & b --><![CDATA[x && y]]></t>")]
    [InlineData("<t>no ampersand</t>")]
    public void Valid_xml_is_left_as_it_is(string svg) =>
        Assert.Null(SvgXmlRepair.FixAmpersands(Encoding.UTF8.GetBytes(svg)));

    [Fact]
    public void Text_in_another_encoding_is_not_touched() =>
        Assert.Null(SvgXmlRepair.FixAmpersands(Encoding.Latin1.GetBytes("<t encoding=\"latin1\">café & co</t>")));

    // ───────────────────────── ICNS ─────────────────────────

    [Fact]
    public void Icns_rle_decodes_literal_and_repeat_runs()
    {
        byte[] data = [.. Enumerable.Range(0, 300).Select(i => (byte)(i < 100 ? i : 7))];

        var decoded = IcnsFile.UnpackBits(GraphicsBuilders.PackBits(data), data.Length);

        Assert.Equal(data, decoded);
    }

    // ───────────────────────── Illustrator ─────────────────────────

    // ───────────────────────── Ghostscript ─────────────────────────

    [Fact]
    public async Task Eps_converts_to_pdf_cropped_and_to_png()
    {
        Assert.SkipUnless(GhostscriptInstalled, "Ghostscript is not installed (pwsh build/fetch-engines.ps1 -Only ghostscript).");
        var dir = Path.Combine(fx.NewFolder(), "한글 폴더");
        Directory.CreateDirectory(dir);
        var eps = Write(dir, "그림 1.eps", GraphicsBuilders.Eps);

        var pdf = await fx.ConvertAsync([eps], new Preset { TargetFormat = "pdf" });
        var png = await fx.ConvertAsync([eps], new Preset { TargetFormat = "png", Pdf = { RenderDpi = 144 } });

        AssertDone(pdf);
        AssertDone(png);
        using (var document = PdfReader.Open(pdf.Outputs.Single(), PdfDocumentOpenMode.Import))
        {
            Assert.Equal(200, document.Pages[0].Width.Point, 1);
            Assert.Equal(100, document.Pages[0].Height.Point, 1);
        }
        using var image = new MagickImage(png.Outputs.Single());
        Assert.Equal((400u, 200u), (image.Width, image.Height));
        var triangle = Pixel(image, 360, 170);
        Assert.True(triangle.B > 150 && triangle.R < 100, triangle.ToString());
        // Where the drawing leaves the page empty, the PNG stays transparent (#39), not white.
        Assert.True(image.HasAlpha);
        Assert.Equal(0, Pixel(image, 2, 2).A);
    }

    [Fact]
    public async Task Pdf_converts_to_eps_per_page_and_to_postscript()
    {
        Assert.SkipUnless(GhostscriptInstalled, "Ghostscript is not installed (pwsh build/fetch-engines.ps1 -Only ghostscript).");
        var dir = fx.NewFolder();
        var pdf = (await fx.ConvertAsync([EngineFixture.MakeMultiPageTiff(dir, 3)], new Preset { TargetFormat = "pdf" })).Outputs.Single();

        var eps = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "eps", Pdf = { PageRange = "2-3" } });
        var ps = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "ps" });

        AssertDone(eps);
        AssertDone(ps);
        Assert.Equal(["pages_p2.eps", "pages_p3.eps"], eps.Outputs.Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.All(eps.Outputs, o => Assert.StartsWith("%!PS-Adobe-3.0 EPSF-3.0", File.ReadAllText(o)));
        var postScript = File.ReadAllText(ps.Outputs.Single());
        Assert.StartsWith("%!PS-Adobe-3.0", postScript);
        Assert.Equal(3, postScript.Split("%%Page:").Length - 1);
    }

    [Fact]
    public async Task Svg_converts_to_eps_through_pdf()
    {
        Assert.SkipUnless(GhostscriptInstalled, "Ghostscript is not installed (pwsh build/fetch-engines.ps1 -Only ghostscript).");
        var dir = fx.NewFolder();
        var svg = Write(dir, "logo.svg", GraphicsBuilders.Svg);

        var job = await fx.ConvertAsync([svg], new Preset { TargetFormat = "eps" });

        AssertDone(job);
        var eps = File.ReadAllText(job.Outputs.Single());
        Assert.StartsWith("%!PS-Adobe-3.0 EPSF-3.0", eps);
        // eps2write crops to the drawn content, which lies inside the 300 × 150 pt page.
        var box = System.Text.RegularExpressions.Regex.Match(eps, @"%%BoundingBox: (\d+) (\d+) (\d+) (\d+)");
        Assert.True(box.Success, "no bounding box");
        var (left, bottom, right, top) = (int.Parse(box.Groups[1].Value), int.Parse(box.Groups[2].Value), int.Parse(box.Groups[3].Value), int.Parse(box.Groups[4].Value));
        Assert.InRange(left, 0, 30);
        Assert.InRange(bottom, 0, 40);
        Assert.InRange(right, 250, 300);
        Assert.InRange(top, 120, 150);
    }

    // ───────────────────────── Routes ─────────────────────────

    [Theory]
    [InlineData("svg", "png", "vector")]
    [InlineData("svg", "pdf", "vector")]
    public void Graphics_routes_use_the_expected_engines(string from, string to, string engines)
    {
        var route = fx.Catalog.CreatePlanner(["ghostscript"]).Plan(from, to);

        Assert.NotNull(route);
        Assert.Equal(engines, string.Join(',', route.Steps.Select(s => s.Converter.Id)));
    }

    [Fact]
    public void Illustrator_headers_are_recognised()
    {
        var dir = fx.NewFolder();
        var pdf = Path.Combine(dir, "a.ai");
        File.WriteAllBytes(pdf, [.. "%PDF-1.6\n"u8]);
        var ps = Write(dir, "b.ai", "%!PS-Adobe-3.0\n");
        var dos = Path.Combine(dir, "c.ai");
        File.WriteAllBytes(dos, [0xC5, 0xD0, 0xD3, 0xC6, 0, 0, 0, 0]);

        Assert.Equal(VectorConverter.AiKind.Pdf, VectorConverter.IllustratorKind(pdf));
        Assert.Equal(VectorConverter.AiKind.PostScript, VectorConverter.IllustratorKind(ps));
        Assert.Equal(VectorConverter.AiKind.PostScript, VectorConverter.IllustratorKind(dos));
    }

    [Fact]
    public void Svgz_detection_uses_the_gzip_signature()
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write("<svg/>"u8);

        Assert.True(SvgRenderer.IsGzip(buffer.ToArray()));
        Assert.False(SvgRenderer.IsGzip("<svg/>"u8));
    }
}

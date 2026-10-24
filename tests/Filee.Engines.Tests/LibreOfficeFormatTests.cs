// Formats only LibreOffice reads (Works / WPS, WordPerfect, AbiWord, Apple iWork, StarOffice, Publisher, CorelDRAW,
// Visio, CGM, ODG): the --convert-to filter of every edge must match the application that opens the source, and
// sample files (AbiWord, ODG written in the test) convert when LibreOffice is installed.

using System.IO.Compression;
using System.Text;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Office;
using ImageMagick;
using UglyToad.PdfPig;

namespace Filee.Engines.Tests;

public class LibreOfficeFormatTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private IConverter LibreOffice => fx.Converters.Single(c => c.Id == "libreoffice");

    private void SkipUnlessInstalled() =>
        Assert.SkipUnless(fx.Catalog.StatusOf(LibreOffice).IsAvailable, "LibreOffice not installed");

    [Theory]
    [InlineData("wpd", "pdf", null, "pdf:writer_pdf_Export")]
    [InlineData("pages", "docx", null, "docx:MS Word 2007 XML")]
    [InlineData("abw", "odt", null, "odt")]
    [InlineData("wps", "txt", null, "txt:Text (encoded):UTF8")]
    [InlineData("numbers", "xlsx", null, "xlsx:Calc MS Excel 2007 XML")]
    [InlineData("sdc", "csv", null, "csv:Text - txt - csv (StarCalc):44,34,76")]
    [InlineData("et", "pdf", null, "pdf:calc_pdf_Export")]
    [InlineData("key", "pptx", null, "pptx:Impress MS PowerPoint 2007 XML")]
    [InlineData("dps", "odp", null, "odp")]
    [InlineData("sda", "pdf", "sdd", "pdf:impress_pdf_Export")]
    [InlineData("sda", "pdf", "sda", "pdf:draw_pdf_Export")]
    [InlineData("cdr", "pdf", null, "pdf:draw_pdf_Export")]
    [InlineData("vsd", "svg", null, "svg:draw_svg_Export")]
    [InlineData("pub", "png", null, "png:draw_png_Export")]
    [InlineData("odg", "odg", null, "odg:draw8")]
    [InlineData("cgm", "odg", null, "odg:impress8_draw")]
    [InlineData("cgm", "svg", null, "svg:impress_svg_Export")]
    [InlineData("emf", "pdf", null, "pdf:draw_pdf_Export")]
    [InlineData("wmf", "png", null, "png:draw_png_Export")]
    [InlineData("cwk", "docx", null, "docx:MS Word 2007 XML")]
    [InlineData("602", "pdf", null, "pdf:writer_pdf_Export")]
    [InlineData("psw", "odt", null, "odt")]
    [InlineData("fods", "xlsx", null, "xlsx:Calc MS Excel 2007 XML")]
    [InlineData("fodp", "pdf", null, "pdf:impress_pdf_Export")]
    [InlineData("fodg", "emf", null, "emf:draw_emf_Export")]
    [InlineData("cgm", "wmf", null, "wmf:impress_wmf_Export")]
    [InlineData("svg", "wmf", null, "wmf:draw_wmf_Export")]
    [InlineData("pdf", "svg", null, "svg:draw_svg_Export")]
    [InlineData("pdf", "odg", null, "odg:draw8")]
    [InlineData("docx", "fodt", null, "fodt:OpenDocument Text Flat XML")]
    [InlineData("odt", "ott", null, "ott:writer8_template")]
    [InlineData("ods", "ots", null, "ots:calc8_template")]
    [InlineData("xlsx", "xlt", null, "xlt:MS Excel 97 Vorlage/Template")]
    [InlineData("pptx", "otp", null, "otp:impress8_template")]
    public void Filters_follow_the_application_that_opens_the_file(string from, string to, string? extension, string expected) =>
        Assert.Equal(expected, LibreOfficeConverter.FilterFor(from, to, pdfA: false, extension));

    private static string DocxText(string docx) =>
        string.Concat(DocxAssert.Paragraphs(DocxAssert.ReadBack(docx).Sections.SelectMany(s => s.Blocks)).Select(DocxAssert.TextOf));

    private static string Mimetype(string package)
    {
        using var zip = ZipFile.OpenRead(package);
        return new StreamReader(zip.GetEntry("mimetype")!.Open()).ReadToEnd();
    }

    private async Task<string> ConvertAsync(string input, string target)
    {
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return job.Outputs.Single();
    }
}

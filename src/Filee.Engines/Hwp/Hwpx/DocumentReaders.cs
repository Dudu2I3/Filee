// The one place that knows how to read a file of some format into the document model (HDocument). The HWPX writer
// (HwpxConverter) and the DOCX writer (DocxWriterConverter) take their input formats from here, so a new reader is one
// line in BuiltIn and both writers (and every route through them) pick it up.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Filee.Engines.Hwp.Hwpx.Docx;
using Filee.Engines.Hwp.Hwpx.Pptx;
using Filee.Engines.Infrastructure;
using Filee.Engines.Office;
using Filee.Engines.Office.Sheets;
using Filee.Engines.Pdf;

namespace Filee.Engines.Hwp.Hwpx;

/// <summary>Registry of document readers: format id → reader.</summary>
internal static class DocumentReaders
{
    /// <summary>
    /// Readers built into Filee: (input file, folder for extracted pictures) → document. Office Open XML variants
    /// (macro-enabled files, templates, slide shows) share the reader of their family.
    /// </summary>
    private static readonly Dictionary<string, Func<string, string, HDocument>> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx"] = DocxReader.Read,
        ["docm"] = DocxReader.Read,
        ["dotx"] = DocxReader.Read,
        ["dotm"] = DocxReader.Read,
        ["txt"] = (path, _) => TextDocument(File.ReadAllBytes(path)),
        ["md"] = (path, _) => MarkdownReader.Read(DecodeText(File.ReadAllBytes(path)), Path.GetDirectoryName(Path.GetFullPath(path))!),
        ["xlsx"] = (path, _) => SheetDocument.Build(XlsxReader.Read(path)),
        ["xlsm"] = (path, _) => SheetDocument.Build(XlsxReader.Read(path)),
        ["xltx"] = (path, _) => SheetDocument.Build(XlsxReader.Read(path)),
        ["csv"] = (path, _) => SheetDocument.Build(CsvFormat.Read(path)),
        ["tsv"] = (path, _) => SheetDocument.Build(SpreadsheetConverter.Read(path, "tsv")),
        ["xls"] = (path, _) => SheetDocument.Build(SpreadsheetConverter.Read(path, "xls")),
        ["xlt"] = (path, _) => SheetDocument.Build(SpreadsheetConverter.Read(path, "xlt")),
        ["ods"] = (path, _) => SheetDocument.Build(SpreadsheetConverter.Read(path, "ods")),
        ["pptx"] = PptxReader.Read,
        ["pptm"] = PptxReader.Read,
        ["potx"] = PptxReader.Read,
        ["potm"] = PptxReader.Read,
        ["ppsx"] = PptxReader.Read,
        ["pdf"] = PdfDocumentReader.Read,
        // HWP comes here as HWPX, converted by rhwp first (the route planner adds that step).
        ["hwpx"] = HwpxReader.Read,
        ["html"] = HtmlReader.Read,
    };

    /// <summary>Formats read without any optional engine.</summary>
    public static IEnumerable<string> BuiltInFormats => BuiltIn.Keys;

    /// <summary>
    /// Extra cost of reading a PDF: its text is rebuilt from positioned words, so a route that keeps the source's own
    /// structure wins (HWP → HWPX → DOCX rather than HWP → PDF → DOCX), while PDF files themselves still convert.
    /// </summary>
    public const int PdfPenalty = 6;

    /// <summary>Reads <paramref name="path"/> (of format <paramref name="format"/>) into the document model.</summary>
    /// <param name="workDirectory">Scratch folder of the job: extracted pictures go into a new folder inside it.</param>
    /// <param name="ocrLayer">A PDF's OCR layer is its text (<see cref="PdfDocumentReader.ReadWithOcrLayer"/>).</param>
    public static async Task<HDocument> ReadAsync(string path, string format, string workDirectory, CancellationToken cancellationToken,
        bool ocrLayer = false)
    {
        var media = Path.Combine(workDirectory, $"media-{Guid.NewGuid():N}");
        if (ocrLayer && format == "pdf")
            return await Task.Run(() => PdfDocumentReader.ReadWithOcrLayer(path, media), cancellationToken);
        if (BuiltIn.TryGetValue(format, out var reader))
            return await Task.Run(() => reader(path, media), cancellationToken);
        throw new NotSupportedException($"There is no document reader for {format.ToUpperInvariant()} files.");
    }

    // ───────────────────────── Plain text ─────────────────────────

    /// <summary>Plain text: one paragraph per line, text kept verbatim.</summary>
    internal static HDocument TextDocument(byte[] bytes)
    {
        var section = new HSection();
        foreach (var line in DecodeText(bytes).Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new HParagraph();
            if (line.Length > 0)
                paragraph.Inlines.Add(new HText(line, default));
            section.Blocks.Add(paragraph);
        }
        var document = new HDocument();
        document.Sections.Add(section);
        return document;
    }

    /// <summary>
    /// Text in whatever encoding its PC used, the way subtitles are read (<see cref="Subtitles.SubtitleEncoding"/>): by
    /// its byte order mark, as UTF-8 when it is valid UTF-8, as CP949 when the bytes look Korean or, on Korean Windows,
    /// are valid CP949; else in the PC's own code page (GBK on Chinese Windows, #491; Shift-JIS on Japanese; Windows-1252
    /// on Western). A <c>charset=</c> in the text is content here, not the file's encoding, so it isn't followed.
    /// </summary>
    internal static string DecodeText(byte[] bytes) => DecodeText(bytes, CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

    /// <summary><see cref="DecodeText(byte[])"/> with the PC's ANSI code page given (for tests).</summary>
    internal static string DecodeText(byte[] bytes, int ansiCodePage) =>
        Subtitles.SubtitleEncoding.Decode(bytes, ansiCodePage, declaredCharset: false);
}

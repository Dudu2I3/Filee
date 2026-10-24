// THE place where conversion engines are registered. Adding an engine = one line in CreateAll.

using Filee.Core.Conversion;
using Filee.Engines.Archives;
using Filee.Engines.Hwp;
using Filee.Engines.Hwp.Hwpx;
using Filee.Engines.Infrastructure;
using Filee.Engines.Magick;
using Filee.Engines.Media;
using Filee.Engines.Ocr;
using Filee.Engines.Office;
using Filee.Engines.Pdf;
using Filee.Engines.Text;
using Filee.Engines.Vector;
using Microsoft.Extensions.Logging;

namespace Filee.Engines;

/// <summary>Creates every built-in converter.</summary>
public static class EngineRegistry
{
    /// <summary>
    /// Instantiates all converters. Order only matters as a tie-breaker when no priority is set, so it mirrors
    /// AppSettings.EnginePriority: built-in engines first, the optional downloads last. Software installed on the
    /// system (Microsoft Office, a system-wide LibreOffice) is never used.
    /// </summary>
    /// <param name="loggers">
    /// The app's log; FFmpeg writes which hardware encoders work and when one failed, LibreOffice when a file needed
    /// its container repacked.
    /// </param>
    public static IReadOnlyList<IConverter> CreateAll(EngineEnvironment env, ILoggerFactory? loggers = null) =>
    [
        new MagickImageConverter(),
        new PdfSharpConverter(),
        new PdfiumConverter(),
        new RhwpConverter(), // draws DOCX / XLSX / PPTX / Markdown / HTML as PDF (through HWPX); HWP files themselves are not offered
        new MarkdownConverter(),
        new DocumentMarkdownConverter(),
        new SpreadsheetConverter(),
        new OoxmlConverter(),
        new HwpxConverter(),
        new ArchiveConverter(),
        new VectorConverter(),
        new DocxWriterConverter(),
        new PdfTextConverter(),
        new PdfSlidesConverter(),
        new PdfTablesConverter(),
        new FfmpegConverter(loggers?.CreateLogger<FfmpegConverter>()),
        new VisionConverter(),
        new LibreOfficeConverter(env, loggers?.CreateLogger<LibreOfficeConverter>()),
    ];

    /// <summary>Finds the PDF merger among the converters.</summary>
    public static IPdfMerger? FindPdfMerger(IEnumerable<IConverter> converters) =>
        converters.OfType<IPdfMerger>().FirstOrDefault();

    /// <summary>Finds the engine that writes several pages into one multi-page TIFF.</summary>
    public static ITiffMerger? FindTiffMerger(IEnumerable<IConverter> converters) =>
        converters.OfType<ITiffMerger>().FirstOrDefault();

    /// <summary>Finds the engine that packs several files into one archive ("Compress into one archive").</summary>
    public static IFileCombiner? FindFileCombiner(IEnumerable<IConverter> converters) =>
        converters.OfType<IFileCombiner>().FirstOrDefault();

    /// <summary>The engine that joins videos, audio files and pictures into one (FFmpeg).</summary>
    public static IMediaJoiner? FindMediaJoiner(IEnumerable<IConverter> converters) =>
        converters.OfType<IMediaJoiner>().FirstOrDefault();
}

// Central list of supported formats and extension lookup.
// To add a new format: add one line to Known and give at least one converter an edge for it.

namespace Filee.Core.Formats;

/// <summary>Lookup table of every format Filee knows about.</summary>
public static class FormatRegistry
{
    /// <summary>Pseudo format of the "Extract" preset: the output is a folder with the archive's files.</summary>
    public const string Folder = "folder";

    /// <summary>All known formats. Order is the order shown in format pickers.</summary>
    public static IReadOnlyList<FileFormat> Known { get; } =
    [
        // Raster images
        new("png", "PNG", FormatCategory.Image, ["png", "png_large"]),
        new("jpg", "JPG", FormatCategory.Image, ["jpg", "jpeg", "jpe", "jfif", "jpg_large"]),
        new("webp", "WEBP", FormatCategory.Image, ["webp"]),
        new("tiff", "TIFF", FormatCategory.Image, ["tiff", "tif"]),
        new("bmp", "BMP", FormatCategory.Image, ["bmp", "dib"]),
        new("gif", "GIF", FormatCategory.Image, ["gif"]),
        new("ico", "ICO", FormatCategory.Image, ["ico"]),
        new("avif", "AVIF", FormatCategory.Image, ["avif"]),
        new("heic", "HEIC", FormatCategory.Image, ["heic", "heif", "hif"]),

        // Vector graphics
        new("svg", "SVG", FormatCategory.Vector, ["svg"]),

        // PDF
        new("pdf", "PDF", FormatCategory.Pdf, ["pdf"]),

        // Word processing
        new("docx", "DOCX", FormatCategory.Document, ["docx"]),
        new("doc", "DOC", FormatCategory.Document, ["doc"]),
        new("odt", "ODT", FormatCategory.Document, ["odt"]),
        new("rtf", "RTF", FormatCategory.Document, ["rtf"]),

        // Internal: the format Filee's built-in document engine writes and rhwp draws as PDF. DOCX / XLSX / PPTX / Markdown /
        // HTML become PDF through it. No donut offers it.
        new("hwpx", "HWPX", FormatCategory.Hwp, ["hwpx"]),

        // Spreadsheets
        new("xlsx", "XLSX", FormatCategory.Spreadsheet, ["xlsx"]),
        new("xls", "XLS", FormatCategory.Spreadsheet, ["xls"]),
        new("csv", "CSV", FormatCategory.Spreadsheet, ["csv"]),

        // Presentations
        new("pptx", "PPTX", FormatCategory.Presentation, ["pptx"]),
        new("ppt", "PPT", FormatCategory.Presentation, ["ppt", "pps", "pot"]),

        // Plain text / markup
        // Text logs, source/proxy scripts and style sheets are read as plain text, never executed (issues #273, #284, #254,
        // #388, #368).
        new("txt", "TXT", FormatCategory.Text, ["txt", "log", "py", "pac", "bat", "command", "lua", "css", "ps1"]),
        new("md", "Markdown", FormatCategory.Text, ["md", "markdown"]),
        new("html", "HTML", FormatCategory.Text, ["html", "htm", "xhtml"]),

        // Video
        new("mp4", "MP4", FormatCategory.Video, ["mp4"]),
        new("m4v", "M4V", FormatCategory.Video, ["m4v"]),
        new("mov", "MOV", FormatCategory.Video, ["mov", "qt"]),
        new("mkv", "MKV", FormatCategory.Video, ["mkv"]),
        new("webm", "WEBM", FormatCategory.Video, ["webm"]),
        new("avi", "AVI", FormatCategory.Video, ["avi", "divx"]),

        // Audio
        new("mp3", "MP3", FormatCategory.Audio, ["mp3"]),
        new("m4a", "M4A", FormatCategory.Audio, ["m4a"]),
        new("aac", "AAC", FormatCategory.Audio, ["aac"]),
        new("wav", "WAV", FormatCategory.Audio, ["wav", "wave"]),
        new("flac", "FLAC", FormatCategory.Audio, ["flac"]),
        new("ogg", "OGG", FormatCategory.Audio, ["ogg", "oga"]),
        new("opus", "OPUS", FormatCategory.Audio, ["opus"]),

        // Archives
        // ANSYS Workbench project archives use the ZIP container (issue #294).
        new("zip", "ZIP", FormatCategory.Archive, ["zip", "wbpz"]),
        new("7z", "7Z", FormatCategory.Archive, ["7z"]),
        new("rar", "RAR", FormatCategory.Archive, ["rar"], Writable: false),
        new("tar", "TAR", FormatCategory.Archive, ["tar"]),
        new("tgz", "TAR.GZ", FormatCategory.Archive, ["tar.gz", "tgz", "taz"]),
        new("gz", "GZ", FormatCategory.Archive, ["gz"]),
        new(Folder, "Folder", FormatCategory.Archive, [""]),
    ];

    private static readonly Dictionary<string, FileFormat> ById =
        Known.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, FileFormat> ByExtension =
        Known.SelectMany(f => f.Extensions.Where(e => e.Length > 0).Select(e => (e, f)))
             .ToDictionary(x => x.e, x => x.f, StringComparer.OrdinalIgnoreCase);

    /// <summary>Extensions made of two parts ("tar.gz"), checked before the last part alone.</summary>
    private static readonly string[] CompoundExtensions =
        ByExtension.Keys.Where(e => e.Contains('.')).OrderByDescending(e => e.Length).ToArray();

    /// <summary>Returns the format with the given id, or <c>null</c>.</summary>
    public static FileFormat? FindById(string id) => ById.GetValueOrDefault(id);

    /// <summary>Returns the format with the given id or throws if it is unknown.</summary>
    public static FileFormat Get(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Unknown format id '{id}'.");

    /// <summary>Returns the format for an extension (with or without the leading dot), or <c>null</c>.</summary>
    public static FileFormat? FindByExtension(string extension) =>
        extension.TrimStart('.') is { Length: > 0 } ext ? ByExtension.GetValueOrDefault(ext) : null;

    /// <summary>Detects the format of a file from its extension, or <c>null</c> if it is not supported.</summary>
    public static FileFormat? Detect(string path) => FindByExtension(ExtensionOf(path));

    /// <summary>
    /// The extension of <paramref name="path"/> without the dot, including known two-part extensions
    /// ("archive.tar.gz" → "tar.gz", "photo.jpg" → "jpg").
    /// </summary>
    public static string ExtensionOf(string path)
    {
        var name = Path.GetFileName(path);
        foreach (var compound in CompoundExtensions)
        {
            if (name.Length > compound.Length + 1
                && name.EndsWith("." + compound, StringComparison.OrdinalIgnoreCase))
                return name[^compound.Length..];
        }
        return Path.GetExtension(name).TrimStart('.');
    }

    /// <summary>The file name without its (possibly two-part) extension: "backup.tar.gz" → "backup".</summary>
    public static string NameWithoutExtension(string path)
    {
        var name = Path.GetFileName(path);
        var extension = ExtensionOf(name);
        return extension.Length == 0 ? name : name[..^(extension.Length + 1)];
    }
}

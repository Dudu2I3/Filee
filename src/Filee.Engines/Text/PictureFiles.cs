using Filee.Engines.Hwp.Hwpx;

namespace Filee.Engines.Text;

/// <summary>Picture helpers for the Markdown files Filee writes (pictures go into a folder beside them).</summary>
internal static class PictureFiles
{
    /// <summary>
    /// A picture in a format every Markdown editor shows: JPEG and PNG (plus GIF and SVG when
    /// <paramref name="gifAndSvg"/>) are kept, others are converted into <paramref name="folder"/> — to JPEG for
    /// opaque photos (WEBP, AVIF, ... would grow a lot as PNG), else PNG. Null when the file cannot be read.
    /// </summary>
    public static (string File, string MediaType)? CommonImage(string path, string folder, bool gifAndSvg)
    {
        var mediaType = ImageMediaType(path);
        if (mediaType is "image/jpeg" or "image/png" || gifAndSvg && mediaType is "image/gif" or "image/svg+xml")
            return (path, mediaType);
        try
        {
            using var image = new ImageMagick.MagickImage(path);
            var jpeg = !image.HasAlpha && mediaType is "image/webp" or "image/avif" or "image/jxl" or "image/tiff";
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{Guid.NewGuid():N}.{(jpeg ? "jpg" : "png")}");
            image.Quality = 90;
            image.Write(file, jpeg ? ImageMagick.MagickFormat.Jpeg : ImageMagick.MagickFormat.Png);
            return (file, jpeg ? "image/jpeg" : "image/png");
        }
        catch (ImageMagick.MagickException)
        {
            return null;
        }
    }

    /// <summary>The media type of a picture file by extension, or null for files that are not pictures.</summary>
    public static string? ImageMediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".avif" => "image/avif",
        ".jxl" => "image/jxl",
        _ => null,
    };
}

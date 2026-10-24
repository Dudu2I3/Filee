// The less common raster formats ImageMagick (Magick.NET Q8) reads and writes for Filee: every one is checked against
// the coders the bundled build really has, then converted there and back. Inputs are generated in the tests.

using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines.Magick;
using ImageMagick;

namespace Filee.Engines.Tests;

public class ImageFormatTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>Formats Filee reads and writes with ImageMagick's own coders (APNG is Filee's, see ApngTests).</summary>
    private static readonly string[] ReadWrite = ["mng", "qoi", "dds", "pcx", "cur", "xpm", "xbm", "wbmp", "pam", "pfm", "sgi", "ras", "miff"];

    private static readonly string[] ReadOnly = ["dcm", "pcd", "exr", "hdr"];

    private static void AssertDone(ConversionJob job)
    {
        var errors = string.Join("; ", job.Files.Where(f => f.State == FileState.Failed).Select(f => $"{f.ErrorKey} {f.ErrorDetail}"));
        Assert.True(job.State == JobState.Completed, $"{job.State}: {errors}");
        Assert.All(job.Outputs, o => Assert.True(new FileInfo(o).Length > 0, o));
    }

    private static MagickImage OnWhite(MagickImage image)
    {
        var flat = (MagickImage)image.Clone();
        flat.BackgroundColor = MagickColors.White;
        flat.Alpha(AlphaOption.Remove);
        flat.ColorSpace = ColorSpace.sRGB;
        return flat;
    }

}

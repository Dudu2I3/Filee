// Formats from issues #301 (raw H.265 / H.264 camera streams) and #328 (HAR network logs) converted for real. The
// video tests need Filee's FFmpeg (`pwsh build/fetch-engines.ps1 -Only ffmpeg`).

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using Filee.Engines.Media;

namespace Filee.Engines.Tests;

[Collection(nameof(FfmpegCollection))]
public class NewExtensionsRealTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static readonly (string Ffmpeg, string Ffprobe)? Tools = FfmpegConverter.Locate();

    private async Task<string> ConvertAsync(string input, string target)
    {
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return Assert.Single(job.Outputs);
    }

    [Fact]
    public void A_raw_stream_is_never_copied_into_a_container()
    {
        var json = """
            {"format": {"format_name": "hevc"}, "streams": [{"codec_type": "video", "codec_name": "hevc", "width": 320,
              "height": 240, "pix_fmt": "yuv420p", "avg_frame_rate": "25/1"}]}
            """;
        var info = MediaInfo.Parse(json);
        Assert.True(info.RawStream);

        var args = MediaEncoding.Plan("in.h265", "out.mp4", "mp4", new MediaOptions(), info, "work").Single().Arguments;
        Assert.Equal("libx264", args[args.ToList().IndexOf("-c:v") + 1]);
        Assert.False(MediaInfo.Parse(json.Replace("\"hevc\"}", "\"mov,mp4,m4a,3gp,3g2,mj2\"}")).RawStream);
    }
}

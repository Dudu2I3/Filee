using ImageMagick;

namespace Filee.Engines.Tests;

/// <summary>Small generated pictures for tests.</summary>
internal static class TestImages
{
    public static byte[] Png(uint width, uint height, MagickColor color)
    {
        using var image = new MagickImage(color, width, height);
        return image.ToByteArray(MagickFormat.Png);
    }
}

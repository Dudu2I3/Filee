// The Liquid Glass donut: contrast of its text on the dense (no native blur) glass, and PNGs of three donuts rendered
// headless into docs/media/lite/. The native blur of macOS is not visible in a headless render; the backdrop is a
// colourful picture so the translucency of the tint shows instead.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class DonutGlassTests
{
    private static double Linear(double channel)
    {
        var c = channel / 255;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static Color Over(Color top, Color bottom)
    {
        var a = top.A / 255.0;
        return Color.FromRgb((byte)Math.Round(top.R * a + bottom.R * (1 - a)), (byte)Math.Round(top.G * a + bottom.G * (1 - a)),
            (byte)Math.Round(top.B * a + bottom.B * (1 - a)));
    }

    private static double Contrast(Color a, Color b)
    {
        var (hi, lo) = (Luminance(a), Luminance(b));
        if (hi < lo)
            (hi, lo) = (lo, hi);
        return (hi + 0.05) / (lo + 0.05);
    }

    [Theory]
    [InlineData(255, 0.0)]   // a white window behind the donut, the sheen not reaching the label
    [InlineData(255, 0.08)]
    [InlineData(255, 0.22)]  // the strongest sheen, in the ring's upper left
    [InlineData(128, 0.22)]
    [InlineData(0, 0.22)]
    public void Labels_keep_their_contrast_on_the_dense_glass(byte backdrop, double sheen)
    {
        var behind = Color.FromRgb(backdrop, backdrop, backdrop);
        var glass = Over(DonutMenu.GlassTintColor(nativeBlur: false), behind);
        glass = Over(Color.FromArgb((byte)Math.Round(255 * sheen), 255, 255, 255), glass);

        Assert.True(Contrast(Colors.White, glass) >= 4.5, $"name {Contrast(Colors.White, glass):0.00}");
        Assert.True(Contrast(Over(DonutMenu.GlassSecondaryColor(nativeBlur: false), glass), glass) >= 4.5,
            $"second line {Contrast(Over(DonutMenu.GlassSecondaryColor(nativeBlur: false), glass), glass):0.00}");

        // The hub has no sheen.
        var hub = Over(DonutMenu.GlassHubTintColor(nativeBlur: false), behind);
        Assert.True(Contrast(Colors.White, hub) >= 4.5);
        Assert.True(Contrast(Over(DonutMenu.GlassSecondaryColor(nativeBlur: false), hub), hub) >= 4.5);
    }

    private static string MediaDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Filee.slnx")))
                dir = dir.Parent;
            var target = Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "docs", "media", "lite");
            Directory.CreateDirectory(target);
            return target;
        }
    }

    private static List<DonutItem> ItemsOf(string profileId, ILocalizer loc, UserDataStore store)
    {
        var profile = store.Profiles.Single(p => p.Id == profileId);
        return profile.PresetIds.Select(store.FindPreset).Where(p => p is not null).Select(p => new DonutItem
        {
            Id = p!.Id,
            Label = loc.DisplayName(p),
            Caption = p.IsGroup || p.IsTool || p.TargetFormat == BuiltInData.SameAsSource ? null : p.TargetFormat.ToUpperInvariant(),
        }).ToList();
    }

    [AvaloniaTheory]
    [InlineData("images", "HEIC", 3, "images")]
    [InlineData("pdf", "PDF", 4, "pdf")]
    [InlineData("video", "MOV", 0, "video")]
    public void Liquid_glass_donut_is_rendered_for_the_docs(string profileId, string source, int highlight, string file)
    {
        TestServices.EnsureInitialized("en");
        var store = AppHost.Get<UserDataStore>();
        var loc = AppHost.Get<ILocalizer>();
        DonutAppearance.Set(DonutStyle.LiquidGlass, nativeBlur: false);

        var donut = new DonutMenu
        {
            Items = ItemsOf(profileId, loc, store),
            OuterRadius = 170,
            HoleRatio = 0.42,
            CenterTitle = $"1 {source}",
            CenterSubtitle = "Esc — close",
            Width = DonutMenu.SideFor(170, false),
            Height = DonutMenu.SideFor(170, false),
        };
        var backdrop = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#3B6BD6"), 0), new GradientStop(Color.Parse("#7B3FA6"), 0.5), new GradientStop(Color.Parse("#E0733C"), 1),
            },
        };
        var side = DonutMenu.SideFor(170, false) + 40;
        var window = new Window
        {
            Width = side,
            Height = side,
            Background = backdrop,
            Content = new Grid { Children = { donut } },
        };
        window.Show();
        try
        {
            RenderTests.Pump();
            donut.SetExternalHighlight(highlight);
            RenderTests.Pump();
            Assert.True(donut.UsesGlass);
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var stream = File.Create(Path.Combine(MediaDirectory, $"donut-{file}.png"));
            frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            frame.Dispose();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_classic_style_stays_available()
    {
        TestServices.EnsureInitialized("en");
        try
        {
            DonutAppearance.Set(DonutStyle.Classic, nativeBlur: false);
            var donut = new DonutMenu();
            Assert.False(donut.UsesGlass);
            DonutAppearance.Set(DonutStyle.LiquidGlass, nativeBlur: false);
            Assert.True(donut.UsesGlass);
            Assert.True(new ThemeSettings().DonutStyle == DonutStyle.LiquidGlass);
            Assert.Equal(ThemeMode.Dark, new ThemeSettings().Mode);
        }
        finally
        {
            DonutAppearance.Set(DonutStyle.LiquidGlass, nativeBlur: false);
        }
    }
}

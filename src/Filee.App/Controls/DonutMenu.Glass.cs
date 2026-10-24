// The Liquid Glass look of the donut: one translucent ring (tint, a sheen from the top left, thin lit edges), thin
// dividers between the slices, a blue sector under the active slice and a glass hub in the middle. The ring is whole:
// slices don't pop out or move, the active one is only lit. Sizes are from the design at an outer radius of 170 and
// scale with the radius. Text is the system font (SF Pro on macOS), white on the glass.
//
// On macOS the blur behind the ring comes from a native view under the window (Filee.Platform.MacOS.MacOSGlass), which
// leaves the tint at 28 %. Without it (other systems, Reduce transparency, a failed attempt) the tint is dense enough
// (82 %) to keep the text readable on any background.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Filee.Core.Settings;

namespace Filee.App.Controls;

/// <summary>How donuts are drawn right now: the style the user chose and whether a native blur sits behind the glass.</summary>
public static class DonutAppearance
{
    public static DonutStyle Style { get; private set; } = DonutStyle.LiquidGlass;

    /// <summary>A native blur view sits behind the donut window and shows through the tint.</summary>
    public static bool NativeBlur { get; private set; }

    /// <summary>Raised when <see cref="Style"/> or <see cref="NativeBlur"/> changed: donuts draw again.</summary>
    public static event Action? Changed;

    public static void Set(DonutStyle style, bool nativeBlur)
    {
        if (Style == style && NativeBlur == nativeBlur)
            return;
        Style = style;
        NativeBlur = nativeBlur;
        Changed?.Invoke();
    }

    public static void SetNativeBlur(bool active) => Set(Style, active);
}

public sealed partial class DonutMenu
{
    /// <summary>Space around the ring for its shadow (and the pop-out of the classic slices).</summary>
    private const double EdgeMargin = 40;

    internal const double GlassLabelSize = 14;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DonutAppearance.Changed += _appearanceChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DonutAppearance.Changed -= _appearanceChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The glass look is on: the live donut in the glass style (the editor and the flip animation stay classic).</summary>
    internal bool UsesGlass => !IsEditMode && !IsFlipping && DonutAppearance.Style == DonutStyle.LiquidGlass;

    // ───────────────────────── Colours (design values) ─────────────────────────

    internal static readonly IBrush GlassWhite = Brushes.White;
    private static readonly IBrush SecondaryNative = new ImmutableSolidColorBrush(GlassSecondaryColor(true));
    private static readonly IBrush SecondaryDense = new ImmutableSolidColorBrush(GlassSecondaryColor(false));
    internal static IBrush GlassSecondary => DonutAppearance.NativeBlur ? SecondaryNative : SecondaryDense;
    internal static readonly IBrush GlassDisabled = new ImmutableSolidColorBrush(Color.FromArgb(77, 235, 235, 245));      // 30 %

    /// <summary>
    /// The ring's tint over the blurred background: 28 % with a native blur. Without one, 90 % of a darker grey than the
    /// design's 82 % of (36, 36, 40): that is what keeps the white names above 4.5:1 on a white background under the
    /// sheen (DonutGlassTests.Labels_keep_their_contrast_on_the_dense_glass).
    /// </summary>
    internal static Color GlassTintColor(bool nativeBlur) =>
        nativeBlur ? Color.FromArgb(71, 44, 44, 50) : Color.FromArgb(230, 30, 30, 34);

    /// <summary>The hub's tint: 30 % over its own blur, as dense as the ring without it.</summary>
    internal static Color GlassHubTintColor(bool nativeBlur) =>
        nativeBlur ? Color.FromArgb(77, 60, 60, 67) : Color.FromArgb(230, 30, 30, 34);

    /// <summary>The second line of a name (and the hub's lines): 62 % white-ish on a blur, 90 % without one to stay readable.</summary>
    internal static Color GlassSecondaryColor(bool nativeBlur) =>
        nativeBlur ? Color.FromArgb(158, 235, 235, 245) : Color.FromArgb(230, 235, 235, 245);

    private static IBrush GlassLabelBrush(bool hot, bool enabled) => !enabled ? GlassDisabled : GlassWhite;

    private static IBrush GlassCaptionBrush(bool hot, bool enabled) => !enabled ? GlassDisabled : hot ? GlassWhite : GlassSecondary;

    private static readonly IBrush TintDense = new ImmutableSolidColorBrush(GlassTintColor(false));
    private static readonly IBrush TintNative = new ImmutableSolidColorBrush(GlassTintColor(true));
    private static readonly IBrush HubDense = new ImmutableSolidColorBrush(GlassHubTintColor(false));
    private static readonly IBrush HubNative = new ImmutableSolidColorBrush(GlassHubTintColor(true));
    private static readonly IPen EdgePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 1);

    private IBrush GlassTint => DonutAppearance.NativeBlur ? TintNative : TintDense;

    /// <summary>The fill of a slice-shaped plate: the glass tint in the glass look, the theme's surface otherwise.</summary>
    private IBrush? SliceFillBrush(double classicOpacity) => UsesGlass ? GlassTint : WithOpacity(SliceBrush, classicOpacity);

    private IPen? SliceEdgePen() => UsesGlass ? EdgePen : BorderPen();

    private IBrush? HubFillBrush() => UsesGlass ? (DonutAppearance.NativeBlur ? HubNative : HubDense) : CenterBrush;

    // ───────────────────────── Drawing ─────────────────────────

    private void RenderGlassRing(DrawingContext context, Point center, double r1, double r2, int n, Typeface typeface, Typeface typefaceRegular)
    {
        var opening = n == 0 || _open.Length == 0 ? 1 : Math.Clamp(_open.Average(), 0, 1);
        using (context.PushOpacity(opening))
            DrawGlassShadow(context, center, r2);

        var scale = 0.88 + 0.12 * opening;
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(center.X, center.Y);
        using var _ = context.PushTransform(transform);
        using var __ = context.PushOpacity(opening);

        var ring = CreateRing(center, r1, r2);
        context.DrawGeometry(GlassTint, null, ring);
        context.DrawGeometry(GlassSheen(center, r2), null, ring);

        // The active sector: only the slice, fading in and out with the highlight.
        for (var i = 0; i < n; i++)
        {
            var item = Items![i];
            if (!item.IsEnabled)
                continue;
            var emphasis = i == _dropTargetIndex ? 1.0 : i < _hover.Length ? _hover[i].Value : 0;
            if (emphasis <= 0.01)
                continue;
            using (context.PushOpacity(Math.Clamp(emphasis, 0, 1)))
                context.DrawGeometry(WithOpacity(AccentBrush, 0.78), null, GlassSector(center, r1, r2, i, n));
        }

        // Dividers on the borders between slices, from the outer edge to the inner one.
        if (n >= 2)
        {
            var half = DonutGeometry.Span(n) / 2;
            for (var i = 0; i < n; i++)
            {
                var angle = DonutGeometry.MidAngle(i, n) + half;
                var from = DonutGeometry.PointAt(center, r2, angle);
                var to = DonutGeometry.PointAt(center, r1, angle);
                var divider = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(from, RelativeUnit.Absolute),
                    EndPoint = new RelativePoint(to, RelativeUnit.Absolute),
                    GradientStops = { new GradientStop(Color.FromArgb(61, 255, 255, 255), 0), new GradientStop(Color.FromArgb(13, 255, 255, 255), 1) },
                };
                context.DrawLine(new Pen(divider, 1), from, to);
            }
        }

        DrawGlassEdges(context, center, r1, r2);

        for (var i = 0; i < n; i++)
        {
            var item = Items![i];
            var mid = DonutGeometry.MidAngle(i, n);
            var span = DonutGeometry.Span(n);
            var hot = (i == ActiveIndex || i == _dropTargetIndex) && item.IsEnabled;
            DrawLabel(context, item, center, (r1 + r2) / 2, mid, span * (r1 + r2) / 2 - 10, r2 - r1 - 16, typeface, typefaceRegular, hot);
        }
    }

    /// <summary>The sheen over the ring: a linear gradient at 150° (towards the lower right), white 22 % → 4 % → 0 → 8 %.</summary>
    private static IBrush GlassSheen(Point center, double r2)
    {
        var direction = new Vector(Math.Sin(150 * Math.PI / 180), -Math.Cos(150 * Math.PI / 180));
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(center - direction * r2, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(center + direction * r2, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(56, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.35),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.58),
                new GradientStop(Color.FromArgb(20, 255, 255, 255), 1),
            },
        };
    }

    /// <summary>
    /// The lit edges: outside a 1.5 px white line at the top (50 %) that fades into a 14 % outline and 8 % at the bottom,
    /// inside a 12 % outline with a 20 % line at the top.
    /// </summary>
    private static void DrawGlassEdges(DrawingContext context, Point center, double r1, double r2)
    {
        IBrush Vertical(double radius, byte top, byte side, byte bottom) => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(new Point(center.X, center.Y - radius), RelativeUnit.Absolute),
            EndPoint = new RelativePoint(new Point(center.X, center.Y + radius), RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(top, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(side, 255, 255, 255), 0.18),
                new GradientStop(Color.FromArgb(side, 255, 255, 255), 0.7),
                new GradientStop(Color.FromArgb(bottom, 255, 255, 255), 1),
            },
        };

        var k = r2 / 170.0;
        context.DrawEllipse(null, new Pen(Vertical(r2, 128, 36, 20), Math.Max(1, 1.5 * k)), center, r2 - 0.5, r2 - 0.5);
        context.DrawEllipse(null, new Pen(Vertical(r1, 51, 31, 31), 1), center, r1 + 0.5, r1 + 0.5);
    }

    private void DrawGlassHub(DrawingContext context, Point center, double radius)
    {
        // Shadow 0 6 18 .35 under the disc.
        var shadow = new RadialGradientBrush
        {
            Center = new RelativePoint(center, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(center, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius + 14, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius + 14, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), (radius - 6) / (radius + 14)),
                new GradientStop(Color.FromArgb(70, 0, 0, 0), (radius - 1) / (radius + 14)),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
            },
        };
        context.DrawEllipse(shadow, null, new Point(center.X, center.Y + 5), radius + 14, radius + 14);

        context.DrawEllipse(HubFillBrush(), null, center, radius, radius);
        var rim = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(new Point(center.X, center.Y - radius), RelativeUnit.Absolute),
            EndPoint = new RelativePoint(new Point(center.X, center.Y + radius), RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(107, 255, 255, 255), 0),   // 42 % highlight at the top
                new GradientStop(Color.FromArgb(31, 255, 255, 255), 0.2),  // 12 % outline
                new GradientStop(Color.FromArgb(31, 255, 255, 255), 1),
            },
        };
        context.DrawEllipse(null, new Pen(rim, 1.5), center, radius - 0.75, radius - 0.75);
    }

    /// <summary>The shadow outside the ring (never under it): 0 30 80 .55 and 0 8 24 .30, as far as the window's margin allows.</summary>
    private void DrawGlassShadow(DrawingContext context, Point center, double r2)
    {
        var extent = EdgeMargin + PopOut;
        var outer = r2 + extent;
        IBrush Falloff(byte peak) => new RadialGradientBrush
        {
            Center = new RelativePoint(center, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(center, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(outer, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(outer, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), (r2 - 6) / outer),
                new GradientStop(Color.FromArgb(peak, 0, 0, 0), (r2 + 1) / outer),
                new GradientStop(Color.FromArgb((byte)(peak * 0.35), 0, 0, 0), (r2 + extent * 0.45) / outer),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
            },
        };
        context.DrawEllipse(Falloff(112), null, new Point(center.X, center.Y + 14), outer, outer);
        context.DrawEllipse(Falloff(60), null, new Point(center.X, center.Y + 4), outer - 10, outer - 10);
    }

    private readonly Dictionary<(Point C, double R1, double R2, int Index, int Count), StreamGeometry> _sectors = new();

    /// <summary>A sharp sector of the ring under slice <paramref name="index"/>, with no gap to its neighbours (the ring is whole).</summary>
    private StreamGeometry GlassSector(Point c, double r1, double r2, int index, int count)
    {
        if (count <= 1)
            return CreateRing(c, r1, r2);
        var key = (c, r1, r2, index, count);
        if (_sectors.TryGetValue(key, out var cached))
            return cached;
        if (_sectors.Count >= MaxCachedWedges)
            _sectors.Clear();
        var outline = DonutGeometry.Wedge(c, r1, r2, DonutGeometry.MidAngle(index, count), DonutGeometry.Span(count), gap: 0);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(outline.Start, true);
            foreach (var step in outline.Steps)
            {
                if (step.IsArc)
                    ctx.ArcTo(step.To, new Size(step.ArcRadius, step.ArcRadius), 0, step.IsLargeArc,
                        step.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                else
                    ctx.LineTo(step.To);
            }
            ctx.EndFigure(true);
        }
        return _sectors[key] = geometry;
    }
}

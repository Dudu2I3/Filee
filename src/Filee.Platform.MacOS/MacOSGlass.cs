using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Filee.Platform.MacOS;

/// <summary>
/// A system blur under the donut window: a native view inserted below Avalonia's content in the window, shaped as the
/// ring (plus a disc under the hub) by a CAShapeLayer mask with the even-odd rule. The Skia layer above draws the tint,
/// sheen, edges and text. NSGlassEffectView (macOS 26) is used when the system has it, else NSVisualEffectView with
/// the HUD window material.
///
/// Nothing here has been run against a real Mac while writing it, so every step is defensive: a missing class or a
/// null view ends in "not attached" and the donut keeps its dense tint. Check the AppKit documentation before relying
/// on it: NSGlassEffectView's API in particular was not verified against Apple's reference.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacOSGlass
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private static readonly nint QuartzCore = TryLoad("/System/Library/Frameworks/QuartzCore.framework/QuartzCore");
    private static readonly Dictionary<nint, nint> Attached = [];

    private static nint TryLoad(string path) => NativeLibrary.TryLoad(path, out var handle) ? handle : 0;

    /// <summary>"Reduce transparency" in Accessibility settings: the glass must not rely on a blur.</summary>
    internal static bool PrefersReducedTransparency()
    {
        var workspace = Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
        return workspace != 0 && SendBool(workspace, sel_registerName("accessibilityDisplayShouldReduceTransparency"));
    }

    /// <summary>
    /// Puts the blur view under the content of <paramref name="window"/> (an NSWindow). True when it is there. The view
    /// fills the window; <see cref="Update"/> shapes it.
    /// </summary>
    internal static bool Attach(nint window, double width, double height)
    {
        if (window == 0)
            return false;
        if (Attached.ContainsKey(window))
            return true;
        var content = Send(window, sel_registerName("contentView"));
        var frameView = content == 0 ? 0 : Send(content, sel_registerName("superview"));
        if (content == 0 || frameView == 0)
            return false;

        var glassClass = OperatingSystem.IsMacOSVersionAtLeast(26) ? objc_getClass("NSGlassEffectView") : 0;
        var useGlass = glassClass != 0;
        var effect = Send(Send(useGlass ? glassClass : objc_getClass("NSVisualEffectView"), sel_registerName("alloc")), sel_registerName("init"));
        if (effect == 0)
            return false;

        if (!useGlass)
        {
            const nint hudWindow = 13, behindWindow = 0, active = 1;
            SendInteger(effect, sel_registerName("setMaterial:"), hudWindow);
            SendInteger(effect, sel_registerName("setBlendingMode:"), behindWindow);
            SendInteger(effect, sel_registerName("setState:"), active);
        }
        const nint widthSizable = 2, heightSizable = 16;
        SendInteger(effect, sel_registerName("setAutoresizingMask:"), widthSizable | heightSizable);
        SendRect(effect, sel_registerName("setFrame:"), new CGRect(0, 0, width, height));
        SendBoolArg(effect, sel_registerName("setWantsLayer:"), true);

        const nint windowBelow = -1;
        SendVoid3(frameView, sel_registerName("addSubview:positioned:relativeTo:"), effect, windowBelow, content);
        Attached[window] = effect;
        return true;
    }

    /// <summary>Shapes the blur as the ring between the radii and a disc of <paramref name="hub"/> under the hub, centred in the window.</summary>
    internal static void Update(nint window, double width, double height, double inner, double outer, double hub)
    {
        if (!Attached.TryGetValue(window, out var effect) || QuartzCore == 0)
            return;
        SendRect(effect, sel_registerName("setFrame:"), new CGRect(0, 0, width, height));
        var layer = Send(effect, sel_registerName("layer"));
        if (layer == 0)
            return;

        var path = CGPathCreateMutable();
        try
        {
            // Even-odd: the outer circle and the inner one leave the ring; the hub's disc inside the hole counts three times.
            foreach (var radius in new[] { outer, inner, hub })
                CGPathAddEllipseInRect(path, 0, new CGRect(width / 2 - radius, height / 2 - radius, 2 * radius, 2 * radius));
            var shape = Send(objc_getClass("CAShapeLayer"), sel_registerName("layer"));
            if (shape == 0)
                return;
            var rule = CFStringCreateWithCString(0, "even-odd", 0x08000100);
            SendVoid(shape, sel_registerName("setFillRule:"), rule);
            CFRelease(rule);
            SendRect(shape, sel_registerName("setFrame:"), new CGRect(0, 0, width, height));
            SendVoid(shape, sel_registerName("setPath:"), path);
            SendVoid(layer, sel_registerName("setMask:"), shape);
        }
        finally
        {
            CGPathRelease(path);
        }
    }

    internal static void Detach(nint window)
    {
        if (!Attached.Remove(window, out var effect))
            return;
        SendVoid(effect, sel_registerName("removeFromSuperview"));
        SendVoid(effect, sel_registerName("release"));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CGRect(double X, double Y, double Width, double Height);

    [DllImport(CoreGraphics)]
    private static extern nint CGPathCreateMutable();

    [DllImport(CoreGraphics)]
    private static extern void CGPathAddEllipseInRect(nint path, nint transform, CGRect rect);

    [DllImport(CoreGraphics)]
    private static extern void CGPathRelease(nint path);

    [DllImport(CoreFoundation)]
    private static extern nint CFStringCreateWithCString(nint allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);

    [DllImport(ObjC)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid3(nint receiver, nint selector, nint first, nint second, nint third);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendInteger(nint receiver, nint selector, nint value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendBoolArg(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendRect(nint receiver, nint selector, CGRect rect);
}

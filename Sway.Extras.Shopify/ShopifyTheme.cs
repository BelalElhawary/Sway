using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Shopify;

/// <summary>
/// Colour tokens for one of the two canvases of the design: the transactional light track (white and cream, with aloe and pistachio
/// greens) or the cinematic night track (black). Aloe and pistachio are surface fills, never text colours, and exist only on the light track: the night colours map them to neutrals.
/// </summary>
public sealed record ShopifyColors
{
    public required Brightness Brightness { get; init; }
    public required SKColor Canvas { get; init; }
    /// <summary>The page background that cards sit on; cream on the light track.</summary>
    public required SKColor CanvasAlt { get; init; }
    public required SKColor Surface { get; init; }
    public required SKColor Ink { get; init; }
    public required SKColor InkSecondary { get; init; }
    public required SKColor InkTertiary { get; init; }
    public required SKColor Hairline { get; init; }
    public required SKColor Primary { get; init; }
    public required SKColor PrimaryPressed { get; init; }
    public required SKColor OnPrimary { get; init; }
    public required SKColor Aloe { get; init; }
    public required SKColor Pistachio { get; init; }
    public required SKColor Shade { get; init; }
    /// <summary>Text and icons on <see cref="Aloe"/>.</summary>
    public required SKColor OnAloe { get; init; }
    /// <summary>Text and icons on <see cref="Pistachio"/>.</summary>
    public required SKColor OnPistachio { get; init; }
    /// <summary>Text and icons on <see cref="Shade"/>.</summary>
    public required SKColor OnShade { get; init; }
    /// <summary>Background of selected text in fields.</summary>
    public required SKColor Selection { get; init; }
    public required SKColor Focus { get; init; }
    public required SKColor Critical { get; init; }
    public required SKColor Disabled { get; init; }
    public required SKColor OnDisabled { get; init; }
    public required SKColor Scrim { get; init; }

    /// <summary>The transactional track: white cards on a cream canvas, black pills.</summary>
    public static ShopifyColors Light { get; } = new()
    {
        Brightness = Brightness.Light,
        Canvas = Colors.FromRgb(0xFFFFFF), CanvasAlt = Colors.FromRgb(0xFBFBF5), Surface = Colors.FromRgb(0xFFFFFF),
        Ink = Colors.FromRgb(0x000000), InkSecondary = Colors.FromRgb(0x52525B), InkTertiary = Colors.FromRgb(0x71717A),
        Hairline = Colors.FromRgb(0xE4E4E7),
        Primary = Colors.FromRgb(0x000000), PrimaryPressed = Colors.FromRgb(0x3F3F46), OnPrimary = Colors.FromRgb(0xFFFFFF),
        Aloe = Colors.FromRgb(0xC1FBD4), Pistachio = Colors.FromRgb(0xD4F9E0), Shade = Colors.FromRgb(0xD4D4D8),
        OnAloe = Colors.Black, OnPistachio = Colors.Black, OnShade = Colors.Black, Selection = Colors.FromRgb(0xC1FBD4),
        Focus = Colors.FromRgb(0x000000), Critical = Colors.FromRgb(0xB42318),
        Disabled = Colors.FromRgb(0xE4E4E7), OnDisabled = Colors.FromRgb(0xA1A1AA), Scrim = Colors.Black.WithOpacity(0.5f),
    };

    /// <summary>The cinematic track: pure black, white type, white-stroked pills.</summary>
    public static ShopifyColors Night { get; } = new()
    {
        Brightness = Brightness.Dark,
        Canvas = Colors.FromRgb(0x000000), CanvasAlt = Colors.FromRgb(0x000000), Surface = Colors.FromRgb(0x0A0A0A),
        Ink = Colors.FromRgb(0xFFFFFF), InkSecondary = Colors.FromRgb(0xA1A1AA), InkTertiary = Colors.FromRgb(0x9DABAD),
        Hairline = Colors.FromRgb(0x1E2C31),
        Primary = Colors.FromRgb(0xFFFFFF), PrimaryPressed = Colors.FromRgb(0xD4D4D8), OnPrimary = Colors.FromRgb(0x000000),
        // The greens are for the light track only, so on black the same tokens resolve to neutrals and nothing green ever appears.
        Aloe = Colors.FromRgb(0xE4E4E7), Pistachio = Colors.FromRgb(0x1E2C31), Shade = Colors.FromRgb(0x3F3F46),
        OnAloe = Colors.Black, OnPistachio = Colors.White, OnShade = Colors.White, Selection = Colors.FromRgb(0x3F3F46),
        Focus = Colors.FromRgb(0xFFFFFF), Critical = Colors.FromRgb(0xFF8A80),
        Disabled = Colors.FromRgb(0x1E2C31), OnDisabled = Colors.FromRgb(0x71717A), Scrim = Colors.Black.WithOpacity(0.7f),
    };
}

/// <summary>
/// The type scale. Display sizes use Neue Haas Grotesk Display at weight 330 when it is installed, else the bundled Inter Display Light
/// (the open stand-in the design names); body and UI text is the bundled Inter. See <see cref="ShopifyFonts"/>.
/// </summary>
public sealed record ShopifyType
{
    public const string Display = "Neue Haas Grotesk Display, Inter Display, Helvetica, Arial, sans-serif";
    public const string Ui = "Inter, Helvetica, Arial, sans-serif";

    static TextStyle D(float size, float line, int weight, float tracking = 0) =>
        new(FontSize: size, Height: line, FontWeight: weight, LetterSpacing: tracking, FontFamily: Display);
    static TextStyle U(float size, float line, int weight, float tracking = 0) =>
        new(FontSize: size, Height: line, FontWeight: weight, LetterSpacing: tracking, FontFamily: Ui);

    public TextStyle DisplayXxl { get; init; } = D(96, 1.0f, 330, 2.4f);
    public TextStyle DisplayXl { get; init; } = D(70, 1.0f, 330);
    public TextStyle DisplayLg { get; init; } = D(55, 1.16f, 330);
    public TextStyle DisplayMd { get; init; } = D(48, 1.14f, 330);
    public TextStyle HeadingXl { get; init; } = D(28, 1.28f, 500, 0.42f);
    public TextStyle HeadingLg { get; init; } = D(24, 1.14f, 400, 0.36f);
    public TextStyle HeadingMd { get; init; } = D(20, 1.4f, 500, 0.3f);
    public TextStyle HeadingSm { get; init; } = D(18, 1.25f, 500, 0.72f);
    public TextStyle BodyLg { get; init; } = U(18, 1.56f, 550);
    public TextStyle BodyMd { get; init; } = U(16, 1.5f, 420);
    public TextStyle BodyStrong { get; init; } = U(16, 1.5f, 550);
    public TextStyle Caption { get; init; } = U(14, 1.49f, 500, 0.28f);
    public TextStyle Micro { get; init; } = U(13, 1.5f, 500, -0.13f);
    public TextStyle Eyebrow { get; init; } = U(12, 1.2f, 400, 0.72f);

    /// <summary>
    /// A display style scaled for the viewport: the hero sizes step down across the breakpoints (96 → 56 on phones, 70 → 44, 55 → 40,
    /// 48 → 36), as the design asks. Tracking is only kept at the full 96px size. Sizes below 48 are left alone.
    /// </summary>
    public TextStyle Responsive(TextStyle desktop, ShopifyBreakpoint breakpoint)
    {
        float size = desktop.FontSize ?? 16;
        if (size < 48) return desktop;
        float scaled = breakpoint switch
        {
            ShopifyBreakpoint.Mobile => size >= 96 ? 56 : size >= 70 ? 44 : size >= 55 ? 40 : 36,
            ShopifyBreakpoint.Tablet => size >= 96 ? 70 : size >= 70 ? 55 : size,
            _ => size,
        };
        return desktop.Merge(new TextStyle(FontSize: scaled, LetterSpacing: scaled >= 96 ? desktop.LetterSpacing : 0));
    }
}

/// <summary>Layout classes from the design: Mobile below 768, Tablet to 1023, Desktop from 1024.</summary>
public enum ShopifyBreakpoint { Mobile, Tablet, Desktop }

public static class ShopifyBreakpoints
{
    public const float TabletMin = 768, DesktopMin = 1024;

    /// <summary>The minimum size of anything tappable, in logical pixels.</summary>
    public const float TouchTarget = 44;

    public static ShopifyBreakpoint For(float width) =>
        width < TabletMin ? ShopifyBreakpoint.Mobile : width < DesktopMin ? ShopifyBreakpoint.Tablet : ShopifyBreakpoint.Desktop;

    /// <summary>Page gutter: 16 on phones, 24 on tablets, 32 on desktops.</summary>
    public static float Gutter(ShopifyBreakpoint b) => b switch { ShopifyBreakpoint.Mobile => 16, ShopifyBreakpoint.Tablet => 24, _ => 32 };

    /// <summary>Product grid columns: 2 on phones, 3 on tablets, 4 on desktops.</summary>
    public static int GridColumns(ShopifyBreakpoint b) => b switch { ShopifyBreakpoint.Mobile => 2, ShopifyBreakpoint.Tablet => 3, _ => 4 };
}

/// <summary>
/// Builds different content for phones, tablets and desktops from the width its parent gives it. The builder also receives the
/// breakpoint and the available width, so components can size themselves without reaching for the window.
/// </summary>
public sealed class ShopifyResponsive(Func<BuildContext, ShopifyBreakpoint, float, Widget> builder, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new LayoutBuilder((ctx, c) =>
    {
        float width = c.HasBoundedWidth ? c.MaxWidth : WidgetsBinding.Instance.RenderView.WindowSize.Width;
        return builder(ctx, ShopifyBreakpoints.For(width), width);
    });
}

/// <summary>A complete theme: colour tokens plus the type scale.</summary>
public sealed record ShopifyThemeData(ShopifyColors Colors, ShopifyType Type)
{
    public Brightness Brightness => Colors.Brightness;
    /// <summary>The transactional track (cream and white).</summary>
    public static ShopifyThemeData Light() => Make(ShopifyColors.Light);
    /// <summary>The cinematic track (black).</summary>
    public static ShopifyThemeData Dark() => Make(ShopifyColors.Night);

    static ShopifyThemeData Make(ShopifyColors colors)
    {
        ShopifyFonts.Register();
        return new ShopifyThemeData(colors, new ShopifyType());
    }
}

/// <summary>Supplies a <see cref="ShopifyThemeData"/> to the subtree. <see cref="ShopifyApp"/> installs one; nest <see cref="Scope"/> to switch track for a section.</summary>
public sealed class ShopifyTheme(ShopifyThemeData data, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public ShopifyThemeData Data { get; } = data;
    public override bool UpdateShouldNotify(InheritedWidget old) => !((ShopifyTheme)old).Data.Equals(Data);

    static readonly ShopifyThemeData Fallback = ShopifyThemeData.Light();

    /// <summary>The nearest theme, rebuilding the caller when it changes; the light track when there is none.</summary>
    public static ShopifyThemeData Of(BuildContext context) => context.DependOn<ShopifyTheme>()?.Data ?? Fallback;

    /// <summary>Installs <paramref name="data"/> together with the matching icon colour and default text style.</summary>
    public static Widget Scope(ShopifyThemeData data, Widget child) =>
        new ShopifyTheme(data, new IconTheme(data.Colors.Ink, 20,
            new DefaultTextStyle(data.Type.BodyMd.Merge(new TextStyle(Color: data.Colors.Ink)), child)));

    /// <summary>Re-establishes the theme inside an overlay, which sits outside the app's scope.</summary>
    internal static Widget Wrap(BuildContext origin, Widget child) =>
        Scope(Of(origin), new Directionality(Directionality.Of(origin), Localizations.Wrap(origin, child)));
}

public enum ShopifyThemeMode { System, Light, Dark }

/// <summary>Root widget for commerce apps: picks the light or dark track (following the OS by default), installs the default text style and paints the canvas.</summary>
public sealed class ShopifyApp(Widget home, ShopifyThemeData? theme = null, ShopifyThemeData? darkTheme = null,
    ShopifyThemeMode themeMode = ShopifyThemeMode.System, Key? key = null) : StatefulWidget(key)
{
    internal Widget Home => home;
    internal ShopifyThemeData? Theme => theme;
    internal ShopifyThemeData? DarkTheme => darkTheme;
    internal ShopifyThemeMode Mode => themeMode;
    public override State CreateState() => new ShopifyAppState();
}

sealed class ShopifyAppState : State<ShopifyApp>
{
    public override void InitState()
    {
        ShopifyFonts.Register();
        WidgetsBinding.Instance.PlatformBrightnessChanged += OnBrightness;
    }
    public override void Dispose() => WidgetsBinding.Instance.PlatformBrightnessChanged -= OnBrightness;
    void OnBrightness() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        bool dark = Widget.Mode switch
        {
            ShopifyThemeMode.Dark => true,
            ShopifyThemeMode.Light => false,
            _ => WidgetsBinding.Instance.PlatformBrightness == Brightness.Dark,
        };
        var data = dark ? Widget.DarkTheme ?? ShopifyThemeData.Dark() : Widget.Theme ?? ShopifyThemeData.Light();
        return ShopifyTheme.Scope(data, new ColoredBox(data.Colors.CanvasAlt, Widget.Home));
    }
}

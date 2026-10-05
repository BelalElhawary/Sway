using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>Carbon's productive type scale in IBM Plex, named as in the Carbon docs. Colours are applied by the widgets.</summary>
public sealed record CarbonType
{
    static TextStyle S(float size, float line, int weight, float tracking, string family = IbmFonts.Sans) =>
        new(FontSize: size, Height: line / size, FontWeight: weight, LetterSpacing: tracking, FontFamily: family + ", sans-serif");

    public TextStyle Code01 { get; init; } = S(12, 16, 400, 0.32f, IbmFonts.Mono);
    public TextStyle Code02 { get; init; } = S(14, 20, 400, 0.32f, IbmFonts.Mono);
    public TextStyle Label01 { get; init; } = S(12, 16, 400, 0.32f);
    public TextStyle Label02 { get; init; } = S(14, 18, 400, 0.16f);
    public TextStyle HelperText01 { get; init; } = S(12, 16, 400, 0.32f);
    public TextStyle HelperText02 { get; init; } = S(14, 18, 400, 0.16f);
    public TextStyle BodyCompact01 { get; init; } = S(14, 18, 400, 0.16f);
    public TextStyle BodyCompact02 { get; init; } = S(16, 22, 400, 0);
    public TextStyle Body01 { get; init; } = S(14, 20, 400, 0.16f);
    public TextStyle Body02 { get; init; } = S(16, 24, 400, 0);
    public TextStyle HeadingCompact01 { get; init; } = S(14, 18, 600, 0.16f);
    public TextStyle HeadingCompact02 { get; init; } = S(16, 22, 600, 0);
    public TextStyle Heading01 { get; init; } = S(14, 20, 600, 0.16f);
    public TextStyle Heading02 { get; init; } = S(16, 24, 600, 0);
    public TextStyle Heading03 { get; init; } = S(20, 28, 400, 0);
    public TextStyle Heading04 { get; init; } = S(28, 36, 400, 0);
    public TextStyle Heading05 { get; init; } = S(32, 40, 400, 0);
    public TextStyle Heading06 { get; init; } = S(42, 50, 300, 0);
    public TextStyle Heading07 { get; init; } = S(54, 64, 300, 0);
    public TextStyle Legal01 { get; init; } = S(12, 16, 400, 0.32f);
}

/// <summary>A complete Carbon theme: its colour tokens and type scale.</summary>
public sealed record CarbonThemeData(CarbonColors Colors, CarbonType Type)
{
    public Brightness Brightness => Colors.Brightness;

    public static CarbonThemeData White() => Make(CarbonColors.White);
    public static CarbonThemeData Gray10() => Make(CarbonColors.Gray10);
    public static CarbonThemeData Gray90() => Make(CarbonColors.Gray90);
    public static CarbonThemeData Gray100() => Make(CarbonColors.Gray100);

    /// <summary>Carbon's light theme (White).</summary>
    public static CarbonThemeData Light() => White();

    /// <summary>Carbon's dark theme (Gray 100).</summary>
    public static CarbonThemeData Dark() => Gray100();

    static CarbonThemeData Make(CarbonColors colors)
    {
        IbmFonts.Register();
        return new CarbonThemeData(colors, new CarbonType());
    }
}

/// <summary>Supplies a <see cref="CarbonThemeData"/> to the subtree. <see cref="CarbonApp"/> installs one for you.</summary>
public sealed class CarbonTheme(CarbonThemeData data, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public CarbonThemeData Data { get; } = data;
    public override bool UpdateShouldNotify(InheritedWidget old) => !ReferenceEquals(((CarbonTheme)old).Data, Data) && !((CarbonTheme)old).Data.Equals(Data);

    static readonly CarbonThemeData Fallback = CarbonThemeData.White();

    /// <summary>The nearest theme, rebuilding the caller when it changes; Carbon White when there is none.</summary>
    public static CarbonThemeData Of(BuildContext context) => context.DependOn<CarbonTheme>()?.Data ?? Fallback;

    /// <summary>Re-establishes the theme, icon colour and text style inside an overlay, which sits outside the app's scope.</summary>
    internal static Widget Wrap(BuildContext origin, Widget child)
    {
        var theme = Of(origin);
        return new CarbonTheme(theme, new IconTheme(theme.Colors.IconPrimary, 16,
            new DefaultTextStyle(theme.Type.BodyCompact01.Merge(new TextStyle(Color: theme.Colors.TextPrimary)),
                new Directionality(Directionality.Of(origin), Localizations.Wrap(origin, child)))));
    }
}

public enum CarbonThemeMode { System, Light, Dark }

/// <summary>
/// Root widget for Carbon apps: picks the light or dark theme (following the operating system by default), installs the
/// default text style and icon colour, and paints the page background. <paramref name="locale"/> selects the language of Carbon's own strings
/// (English when null; <see cref="Locale.System"/> follows the OS) and the text direction unless <paramref name="textDirection"/> is given.
/// </summary>
public sealed class CarbonApp(Widget home, CarbonThemeData? theme = null, CarbonThemeData? darkTheme = null,
    CarbonThemeMode themeMode = CarbonThemeMode.System, TextDirection? textDirection = null, Locale? locale = null,
    IReadOnlyList<ILocalizationsDelegate>? localizationsDelegates = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget Home => home;
    internal CarbonThemeData? Theme => theme;
    internal CarbonThemeData? DarkTheme => darkTheme;
    internal CarbonThemeMode Mode => themeMode;
    internal TextDirection? Direction => textDirection ?? locale?.TextDirection;
    internal Locale Locale => locale ?? Locale.English;
    internal IReadOnlyList<ILocalizationsDelegate> Delegates { get; } = [CarbonLocalizations.Delegate, ..localizationsDelegates ?? []];
    public override State CreateState() => new CarbonAppState();
}

sealed class CarbonAppState : State<CarbonApp>
{
    public override void InitState() => WidgetsBinding.Instance.PlatformBrightnessChanged += OnBrightness;
    public override void Dispose() => WidgetsBinding.Instance.PlatformBrightnessChanged -= OnBrightness;
    void OnBrightness() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        bool dark = Widget.Mode switch
        {
            CarbonThemeMode.Dark => true,
            CarbonThemeMode.Light => false,
            _ => WidgetsBinding.Instance.PlatformBrightness == Brightness.Dark,
        };
        var data = dark ? Widget.DarkTheme ?? CarbonThemeData.Dark() : Widget.Theme ?? CarbonThemeData.Light();
        Widget app = new CarbonTheme(data, new IconTheme(data.Colors.IconPrimary, 16,
            new DefaultTextStyle(data.Type.BodyCompact01.Merge(new TextStyle(Color: data.Colors.TextPrimary)),
                new ColoredBox(data.Colors.Background, Widget.Home))));
        app = new Localizations(Widget.Locale, Widget.Delegates, app);
        if (Widget.Direction is { } d) app = new Directionality(d, app);
        return app;
    }
}

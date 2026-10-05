using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>
/// Root widget for Material apps: picks light or dark theme, installs the default text style and paints the surface. <paramref name="locale"/>
/// selects the language of Material's own strings and date formats (English when null; <see cref="Locale.System"/> follows the OS) and sets the text
/// direction unless <paramref name="textDirection"/> is given. <paramref name="localizationsDelegates"/> adds app resources, read with
/// <see cref="Localizations.Of{T}"/>.
/// </summary>
public sealed class MaterialApp(Widget home, ThemeData? theme = null, ThemeData? darkTheme = null, ThemeMode themeMode = ThemeMode.System,
    TextDirection? textDirection = null, Locale? locale = null, IReadOnlyList<ILocalizationsDelegate>? localizationsDelegates = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget Home => home;
    internal ThemeData? Theme => theme;
    internal ThemeData? DarkTheme => darkTheme;
    internal ThemeMode Mode => themeMode;
    internal TextDirection? Direction => textDirection ?? locale?.TextDirection;
    internal IReadOnlyList<ILocalizationsDelegate> Delegates { get; } = [MaterialLocalizations.Delegate, ..localizationsDelegates ?? []];
    internal Locale Locale => locale ?? Locale.English;
    public override State CreateState() => new MaterialAppState();
}

sealed class MaterialAppState : State<MaterialApp>
{
    public override void InitState() => WidgetsBinding.Instance.PlatformBrightnessChanged += OnBrightness;
    public override void Dispose() => WidgetsBinding.Instance.PlatformBrightnessChanged -= OnBrightness;
    void OnBrightness() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        bool dark = Widget.Mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => WidgetsBinding.Instance.PlatformBrightness == Brightness.Dark,
        };
        var data = dark ? Widget.DarkTheme ?? ThemeData.Dark() : Widget.Theme ?? ThemeData.Light();
        Widget app = new Theme(data, new IconTheme(data.ColorScheme.OnSurfaceVariant, 24,
            new DefaultTextStyle(data.TextTheme.BodyMedium, new ColoredBox(data.ScaffoldBackground, Widget.Home))));
        app = new Localizations(Widget.Locale, Widget.Delegates, app);
        if (Widget.Direction is { } d) app = new Directionality(d, app);
        return app;
    }
}

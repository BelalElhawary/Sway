using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>
/// Carbon, IBM's design system, as Sway themes: the four Carbon colour themes (White and Gray 10 are light, Gray 90 and
/// Gray 100 are dark), the IBM Plex type scale, and Carbon's square, flat, compact shape.
/// </summary>
/// <remarks>
/// Carbon tokens are mapped onto the Material colour roles the widgets already read, so every widget picks the theme up
/// without changes. Pass the light and dark themes to <see cref="MaterialApp"/> as usual:
/// <c>new MaterialApp(home, theme: IbmTheme.White(), darkTheme: IbmTheme.Gray100())</c>.
/// </remarks>
public static class IbmTheme
{
    /// <summary>Square corners, no shadows, 40px controls: the Carbon "productive" density.</summary>
    public static readonly ShapeTheme Shape = new()
    {
        ExtraSmall = 0, Small = 0, Medium = 0, Large = 0, ExtraLarge = 0, Button = 0, Round = 0,
        ControlHeight = 40, ButtonPadding = 16, FieldHeight = 40, Flat = true,
        CheckboxRadius = 0, ControlHalo = false, CompactSwitch = true,
    };

    public static ThemeData White() => Build(CarbonColors.White);
    public static ThemeData Gray10() => Build(CarbonColors.Gray10);
    public static ThemeData Gray90() => Build(CarbonColors.Gray90);
    public static ThemeData Gray100() => Build(CarbonColors.Gray100);

    /// <summary>Carbon's light theme (White).</summary>
    public static ThemeData Light() => White();

    /// <summary>Carbon's dark theme (Gray 100).</summary>
    public static ThemeData Dark() => Gray100();

    /// <summary>The Carbon theme with the given brightness; <paramref name="gray"/> picks the tinted variant (Gray 10 or Gray 90).</summary>
    public static ThemeData For(Brightness brightness, bool gray = false) => brightness == Brightness.Dark
        ? gray ? Gray90() : Gray100()
        : gray ? Gray10() : White();

    static ThemeData Build(ColorScheme scheme)
    {
        IbmFonts.Register();
        return new ThemeData { ColorScheme = scheme, TextTheme = Type().Apply(scheme.OnSurface), Shape = Shape };
    }

    static TextStyle S(float size, float line, int weight, float tracking) =>
        new(FontSize: size, Height: line / size, FontWeight: weight, LetterSpacing: tracking, FontFamily: IbmFonts.Sans + ", sans-serif");

    /// <summary>The IBM Plex type scale: Carbon's productive styles mapped onto the Material roles (body-compact for UI text, heading-compact for titles).</summary>
    public static TextTheme Type() => new()
    {
        DisplayLarge = S(76, 84, 300, -0.64f),
        DisplayMedium = S(60, 70, 300, 0),
        DisplaySmall = S(54, 64, 300, 0),
        HeadlineLarge = S(42, 50, 300, 0),
        HeadlineMedium = S(32, 40, 400, 0),
        HeadlineSmall = S(28, 36, 400, 0),
        TitleLarge = S(20, 28, 400, 0),
        TitleMedium = S(16, 22, 600, 0),
        TitleSmall = S(14, 18, 600, 0.16f),
        BodyLarge = S(16, 24, 400, 0),
        BodyMedium = S(14, 18, 400, 0.16f),
        BodySmall = S(12, 16, 400, 0.32f),
        LabelLarge = S(14, 18, 400, 0.16f),
        LabelMedium = S(12, 16, 400, 0.32f),
        LabelSmall = S(11, 16, 400, 0.32f),
    };
}

using SkiaSharp;

namespace Sway.Widgets;

public enum Brightness { Light, Dark }

/// <summary>
/// A tone ramp for one hue, generated in CIE L*C*h so tone N has lightness N. This approximates Material's HCT
/// palettes closely enough for seeded schemes; the default baseline schemes use the published exact values.
/// </summary>
public sealed class TonalPalette(float hue, float chroma)
{
    public SKColor Tone(int tone)
    {
        if (tone <= 0) return SKColors.Black;
        if (tone >= 100) return SKColors.White;
        // Reduce chroma until the colour fits in sRGB so tones stay on-hue instead of clipping.
        for (float c = chroma; c >= 0; c -= 1)
            if (TryLab(tone, c, hue, out var color)) return color;
        return LabToColor(tone, 0, hue);
    }

    static bool TryLab(float l, float c, float h, out SKColor color)
    {
        var (r, g, b) = LabToLinear(l, c, h);
        const float eps = 0.0005f;
        bool inGamut = r >= -eps && r <= 1 + eps && g >= -eps && g <= 1 + eps && b >= -eps && b <= 1 + eps;
        color = FromLinear(r, g, b);
        return inGamut;
    }

    static SKColor LabToColor(float l, float c, float h)
    {
        var (r, g, b) = LabToLinear(l, c, h);
        return FromLinear(r, g, b);
    }

    static (float r, float g, float b) LabToLinear(float l, float c, float hDeg)
    {
        float hr = hDeg * MathF.PI / 180, a = c * MathF.Cos(hr), bb = c * MathF.Sin(hr);
        float fy = (l + 16) / 116, fx = fy + a / 500, fz = fy - bb / 200;
        static float Inv(float t) => t * t * t > 0.008856f ? t * t * t : (t - 16f / 116) / 7.787f;
        float x = 0.95047f * Inv(fx), y = Inv(fy), z = 1.08883f * Inv(fz);
        return (3.2404542f * x - 1.5371385f * y - 0.4985314f * z,
                -0.9692660f * x + 1.8760108f * y + 0.0415560f * z,
                0.0556434f * x - 0.2040259f * y + 1.0572252f * z);
    }

    static SKColor FromLinear(float r, float g, float b)
    {
        static byte Enc(float v)
        {
            v = Math.Clamp(v, 0, 1);
            float s = v <= 0.0031308f ? 12.92f * v : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f;
            return (byte)MathF.Round(Math.Clamp(s, 0, 1) * 255);
        }
        return new SKColor(Enc(r), Enc(g), Enc(b));
    }

    /// <summary>The hue (degrees) and chroma of a colour in CIE L*C*h.</summary>
    public static (float hue, float chroma) HueChroma(SKColor color)
    {
        static float Lin(byte v) { float s = v / 255f; return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f); }
        float r = Lin(color.Red), g = Lin(color.Green), b = Lin(color.Blue);
        float x = (0.4124564f * r + 0.3575761f * g + 0.1804375f * b) / 0.95047f;
        float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
        float z = (0.0193339f * r + 0.1191920f * g + 0.9503041f * b) / 1.08883f;
        static float F(float t) => t > 0.008856f ? MathF.Cbrt(t) : 7.787f * t + 16f / 116;
        float fx = F(x), fy = F(y), fz = F(z);
        float a = 500 * (fx - fy), bb = 200 * (fy - fz);
        float h = MathF.Atan2(bb, a) * 180 / MathF.PI;
        return ((h + 360) % 360, MathF.Sqrt(a * a + bb * bb));
    }
}

/// <summary>The Material 3 colour roles. Defaults are the baseline (purple) light scheme.</summary>
public sealed record ColorScheme
{
    public Brightness Brightness { get; init; } = Brightness.Light;

    public SKColor Primary { get; init; } = Colors.FromRgb(0x6750A4);
    public SKColor OnPrimary { get; init; } = Colors.FromRgb(0xFFFFFF);
    public SKColor PrimaryContainer { get; init; } = Colors.FromRgb(0xEADDFF);
    public SKColor OnPrimaryContainer { get; init; } = Colors.FromRgb(0x21005D);
    public SKColor Secondary { get; init; } = Colors.FromRgb(0x625B71);
    public SKColor OnSecondary { get; init; } = Colors.FromRgb(0xFFFFFF);
    public SKColor SecondaryContainer { get; init; } = Colors.FromRgb(0xE8DEF8);
    public SKColor OnSecondaryContainer { get; init; } = Colors.FromRgb(0x1D192B);
    public SKColor Tertiary { get; init; } = Colors.FromRgb(0x7D5260);
    public SKColor OnTertiary { get; init; } = Colors.FromRgb(0xFFFFFF);
    public SKColor TertiaryContainer { get; init; } = Colors.FromRgb(0xFFD8E4);
    public SKColor OnTertiaryContainer { get; init; } = Colors.FromRgb(0x31111D);
    public SKColor Error { get; init; } = Colors.FromRgb(0xB3261E);
    public SKColor OnError { get; init; } = Colors.FromRgb(0xFFFFFF);
    public SKColor ErrorContainer { get; init; } = Colors.FromRgb(0xF9DEDC);
    public SKColor OnErrorContainer { get; init; } = Colors.FromRgb(0x410E0B);
    public SKColor Surface { get; init; } = Colors.FromRgb(0xFEF7FF);
    public SKColor OnSurface { get; init; } = Colors.FromRgb(0x1D1B20);
    public SKColor SurfaceVariant { get; init; } = Colors.FromRgb(0xE7E0EC);
    public SKColor OnSurfaceVariant { get; init; } = Colors.FromRgb(0x49454F);
    public SKColor Outline { get; init; } = Colors.FromRgb(0x79747E);
    public SKColor OutlineVariant { get; init; } = Colors.FromRgb(0xCAC4D0);
    public SKColor Shadow { get; init; } = Colors.FromRgb(0x000000);
    public SKColor Scrim { get; init; } = Colors.FromRgb(0x000000);
    public SKColor InverseSurface { get; init; } = Colors.FromRgb(0x322F35);
    public SKColor OnInverseSurface { get; init; } = Colors.FromRgb(0xF5EFF7);
    public SKColor InversePrimary { get; init; } = Colors.FromRgb(0xD0BCFF);
    public SKColor SurfaceDim { get; init; } = Colors.FromRgb(0xDED8E1);
    public SKColor SurfaceBright { get; init; } = Colors.FromRgb(0xFEF7FF);
    public SKColor SurfaceContainerLowest { get; init; } = Colors.FromRgb(0xFFFFFF);
    public SKColor SurfaceContainerLow { get; init; } = Colors.FromRgb(0xF7F2FA);
    public SKColor SurfaceContainer { get; init; } = Colors.FromRgb(0xF3EDF7);
    public SKColor SurfaceContainerHigh { get; init; } = Colors.FromRgb(0xECE6F0);
    public SKColor SurfaceContainerHighest { get; init; } = Colors.FromRgb(0xE6E0E9);

    /// <summary>Material 3 baseline light scheme.</summary>
    public static readonly ColorScheme Light = new();

    /// <summary>Material 3 baseline dark scheme.</summary>
    public static readonly ColorScheme Dark = new()
    {
        Brightness = Brightness.Dark,
        Primary = Colors.FromRgb(0xD0BCFF), OnPrimary = Colors.FromRgb(0x381E72),
        PrimaryContainer = Colors.FromRgb(0x4F378B), OnPrimaryContainer = Colors.FromRgb(0xEADDFF),
        Secondary = Colors.FromRgb(0xCCC2DC), OnSecondary = Colors.FromRgb(0x332D41),
        SecondaryContainer = Colors.FromRgb(0x4A4458), OnSecondaryContainer = Colors.FromRgb(0xE8DEF8),
        Tertiary = Colors.FromRgb(0xEFB8C8), OnTertiary = Colors.FromRgb(0x492532),
        TertiaryContainer = Colors.FromRgb(0x633B48), OnTertiaryContainer = Colors.FromRgb(0xFFD8E4),
        Error = Colors.FromRgb(0xF2B8B5), OnError = Colors.FromRgb(0x601410),
        ErrorContainer = Colors.FromRgb(0x8C1D18), OnErrorContainer = Colors.FromRgb(0xF9DEDC),
        Surface = Colors.FromRgb(0x141218), OnSurface = Colors.FromRgb(0xE6E0E9),
        SurfaceVariant = Colors.FromRgb(0x49454F), OnSurfaceVariant = Colors.FromRgb(0xCAC4D0),
        Outline = Colors.FromRgb(0x938F99), OutlineVariant = Colors.FromRgb(0x49454F),
        InverseSurface = Colors.FromRgb(0xE6E0E9), OnInverseSurface = Colors.FromRgb(0x322F35), InversePrimary = Colors.FromRgb(0x6750A4),
        SurfaceDim = Colors.FromRgb(0x141218), SurfaceBright = Colors.FromRgb(0x3B383E),
        SurfaceContainerLowest = Colors.FromRgb(0x0F0D13), SurfaceContainerLow = Colors.FromRgb(0x1D1B20),
        SurfaceContainer = Colors.FromRgb(0x211F26), SurfaceContainerHigh = Colors.FromRgb(0x2B2930), SurfaceContainerHighest = Colors.FromRgb(0x36343B),
    };

    /// <summary>Generates a full scheme from one seed colour (primary hue), following Material's tone mapping.</summary>
    public static ColorScheme FromSeed(SKColor seed, Brightness brightness = Brightness.Light)
    {
        var (hue, chroma) = TonalPalette.HueChroma(seed);
        var p = new TonalPalette(hue, Math.Max(48, chroma));
        var s = new TonalPalette(hue, 16);
        var t = new TonalPalette((hue + 60) % 360, 24);
        var n = new TonalPalette(hue, 4);
        var nv = new TonalPalette(hue, 8);
        var e = new TonalPalette(25, 84);

        return brightness == Brightness.Light
            ? new ColorScheme
            {
                Brightness = Brightness.Light,
                Primary = p.Tone(40), OnPrimary = p.Tone(100), PrimaryContainer = p.Tone(90), OnPrimaryContainer = p.Tone(10),
                Secondary = s.Tone(40), OnSecondary = s.Tone(100), SecondaryContainer = s.Tone(90), OnSecondaryContainer = s.Tone(10),
                Tertiary = t.Tone(40), OnTertiary = t.Tone(100), TertiaryContainer = t.Tone(90), OnTertiaryContainer = t.Tone(10),
                Error = e.Tone(40), OnError = e.Tone(100), ErrorContainer = e.Tone(90), OnErrorContainer = e.Tone(10),
                Surface = n.Tone(98), OnSurface = n.Tone(10), SurfaceVariant = nv.Tone(90), OnSurfaceVariant = nv.Tone(30),
                Outline = nv.Tone(50), OutlineVariant = nv.Tone(80),
                InverseSurface = n.Tone(20), OnInverseSurface = n.Tone(95), InversePrimary = p.Tone(80),
                SurfaceDim = n.Tone(87), SurfaceBright = n.Tone(98),
                SurfaceContainerLowest = n.Tone(100), SurfaceContainerLow = n.Tone(96), SurfaceContainer = n.Tone(94),
                SurfaceContainerHigh = n.Tone(92), SurfaceContainerHighest = n.Tone(90),
            }
            : new ColorScheme
            {
                Brightness = Brightness.Dark,
                Primary = p.Tone(80), OnPrimary = p.Tone(20), PrimaryContainer = p.Tone(30), OnPrimaryContainer = p.Tone(90),
                Secondary = s.Tone(80), OnSecondary = s.Tone(20), SecondaryContainer = s.Tone(30), OnSecondaryContainer = s.Tone(90),
                Tertiary = t.Tone(80), OnTertiary = t.Tone(20), TertiaryContainer = t.Tone(30), OnTertiaryContainer = t.Tone(90),
                Error = e.Tone(80), OnError = e.Tone(20), ErrorContainer = e.Tone(30), OnErrorContainer = e.Tone(90),
                Surface = n.Tone(6), OnSurface = n.Tone(90), SurfaceVariant = nv.Tone(30), OnSurfaceVariant = nv.Tone(80),
                Outline = nv.Tone(60), OutlineVariant = nv.Tone(30),
                InverseSurface = n.Tone(90), OnInverseSurface = n.Tone(20), InversePrimary = p.Tone(40),
                SurfaceDim = n.Tone(6), SurfaceBright = n.Tone(24),
                SurfaceContainerLowest = n.Tone(4), SurfaceContainerLow = n.Tone(10), SurfaceContainer = n.Tone(12),
                SurfaceContainerHigh = n.Tone(17), SurfaceContainerHighest = n.Tone(22),
            };
    }

    public SKColor Background => Surface;
    public SKColor OnBackground => OnSurface;
}

/// <summary>The Material 3 type scale. Fonts fall back to Segoe UI when Roboto is not installed.</summary>
public sealed record TextTheme
{
    const string Family = "Roboto, Segoe UI, sans-serif";

    static TextStyle S(float size, float line, int weight, float tracking) =>
        new(FontSize: size, Height: line / size, FontWeight: weight, LetterSpacing: tracking, FontFamily: Family);

    public TextStyle DisplayLarge { get; init; } = S(57, 64, 400, -0.25f);
    public TextStyle DisplayMedium { get; init; } = S(45, 52, 400, 0);
    public TextStyle DisplaySmall { get; init; } = S(36, 44, 400, 0);
    public TextStyle HeadlineLarge { get; init; } = S(32, 40, 400, 0);
    public TextStyle HeadlineMedium { get; init; } = S(28, 36, 400, 0);
    public TextStyle HeadlineSmall { get; init; } = S(24, 32, 400, 0);
    public TextStyle TitleLarge { get; init; } = S(22, 28, 400, 0);
    public TextStyle TitleMedium { get; init; } = S(16, 24, 500, 0.15f);
    public TextStyle TitleSmall { get; init; } = S(14, 20, 500, 0.1f);
    public TextStyle BodyLarge { get; init; } = S(16, 24, 400, 0.5f);
    public TextStyle BodyMedium { get; init; } = S(14, 20, 400, 0.25f);
    public TextStyle BodySmall { get; init; } = S(12, 16, 400, 0.4f);
    public TextStyle LabelLarge { get; init; } = S(14, 20, 500, 0.1f);
    public TextStyle LabelMedium { get; init; } = S(12, 16, 500, 0.5f);
    public TextStyle LabelSmall { get; init; } = S(11, 16, 500, 0.5f);

    /// <summary>Applies colour to every style (used so text defaults to on-surface).</summary>
    public TextTheme Apply(SKColor color, SKColor? displayColor = null)
    {
        TextStyle C(TextStyle s) => s with { Color = color };
        return this with
        {
            DisplayLarge = C(DisplayLarge), DisplayMedium = C(DisplayMedium), DisplaySmall = C(DisplaySmall),
            HeadlineLarge = C(HeadlineLarge), HeadlineMedium = C(HeadlineMedium), HeadlineSmall = C(HeadlineSmall),
            TitleLarge = C(TitleLarge), TitleMedium = C(TitleMedium), TitleSmall = C(TitleSmall),
            BodyLarge = C(BodyLarge), BodyMedium = C(BodyMedium), BodySmall = C(BodySmall),
            LabelLarge = C(LabelLarge), LabelMedium = C(LabelMedium), LabelSmall = C(LabelSmall),
        };
    }
}

/// <summary>Material 3 shape scale (corner radii in logical pixels).</summary>
public static class Shapes
{
    public const float None = 0, ExtraSmall = 4, Small = 8, Medium = 12, Large = 16, ExtraLarge = 28, Full = 1000;
}

/// <summary>Material 3 elevation: the two-layer shadow for each level (0 to 5).</summary>
public static class Elevation
{
    public static IReadOnlyList<BoxShadow>? Shadows(int level, SKColor shadow)
    {
        if (level <= 0) return null;
        var umbra = shadow.WithOpacity(0.30f);
        var penumbra = shadow.WithOpacity(0.15f);
        return level switch
        {
            1 => [new BoxShadow(umbra, new Offset(0, 1), 2), new BoxShadow(penumbra, new Offset(0, 1), 3, 1)],
            2 => [new BoxShadow(umbra, new Offset(0, 1), 2), new BoxShadow(penumbra, new Offset(0, 2), 6, 2)],
            3 => [new BoxShadow(umbra, new Offset(0, 1), 3), new BoxShadow(penumbra, new Offset(0, 4), 8, 3)],
            4 => [new BoxShadow(umbra, new Offset(0, 2), 3), new BoxShadow(penumbra, new Offset(0, 6), 10, 4)],
            _ => [new BoxShadow(umbra, new Offset(0, 4), 4), new BoxShadow(penumbra, new Offset(0, 8), 12, 6)],
        };
    }
}

public sealed record ThemeData
{
    public ColorScheme ColorScheme { get; init; } = ColorScheme.Light;
    public TextTheme TextTheme { get; init; } = new TextTheme().Apply(ColorScheme.Light.OnSurface);
    public bool UseMaterial3 { get; init; } = true;
    public Brightness Brightness => ColorScheme.Brightness;

    public SKColor ScaffoldBackground => ColorScheme.Surface;

    /// <summary>The default Material 3 light theme (baseline purple).</summary>
    public static ThemeData Light() => FromScheme(ColorScheme.Light);

    /// <summary>The default Material 3 dark theme.</summary>
    public static ThemeData Dark() => FromScheme(ColorScheme.Dark);

    public static ThemeData FromScheme(ColorScheme scheme) =>
        new() { ColorScheme = scheme, TextTheme = new TextTheme().Apply(scheme.OnSurface) };

    public static ThemeData FromSeed(SKColor seed, Brightness brightness = Brightness.Light) =>
        FromScheme(ColorScheme.FromSeed(seed, brightness));
}

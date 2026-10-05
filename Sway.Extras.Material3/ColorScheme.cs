using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

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

using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>The four Carbon colour themes as <see cref="ColorScheme"/>s.</summary>
/// <remarks>
/// Values are Carbon's published tokens, mapped onto the Material roles the widgets read: Primary is button-primary,
/// Secondary is button-secondary, Outline is border-strong, OutlineVariant is border-subtle, SecondaryContainer is
/// layer-selected, and the SurfaceContainer ramp is the layer ramp. Accent is the link/ghost-button blue, which is lighter
/// than the button fill on the dark themes.
/// </remarks>
public static class CarbonColors
{
    static SKColor C(uint rgb) => Colors.FromRgb(rgb);

    static ColorScheme Light(uint background, uint layer01, uint layer02) => new()
    {
        Brightness = Brightness.Light,
        Primary = C(0x0F62FE), Accent = C(0x0F62FE), OnPrimary = C(0xFFFFFF), PrimaryContainer = C(0xD0E2FF), OnPrimaryContainer = C(0x001D6C),
        Secondary = C(0x393939), OnSecondary = C(0xFFFFFF), SecondaryContainer = C(0xE0E0E0), OnSecondaryContainer = C(0x161616),
        Tertiary = C(0x8A3FFC), OnTertiary = C(0xFFFFFF), TertiaryContainer = C(0xE8DAFF), OnTertiaryContainer = C(0x31135E),
        Error = C(0xDA1E28), OnError = C(0xFFFFFF), ErrorContainer = C(0xFFF1F1), OnErrorContainer = C(0x520408),
        Surface = C(background), OnSurface = C(0x161616), SurfaceVariant = C(layer01), OnSurfaceVariant = C(0x525252),
        Outline = C(0x8D8D8D), OutlineVariant = C(0xE0E0E0),
        Shadow = C(0x000000), Scrim = C(0x161616),
        InverseSurface = C(0x393939), OnInverseSurface = C(0xFFFFFF), InversePrimary = C(0x78A9FF),
        SurfaceDim = C(0xE0E0E0), SurfaceBright = C(background),
        SurfaceContainerLowest = C(background), SurfaceContainerLow = C(layer01), SurfaceContainer = C(layer01),
        SurfaceContainerHigh = C(layer02), SurfaceContainerHighest = C(layer01),
    };

    static ColorScheme Dark(uint background, uint layer01, uint layer02) => new()
    {
        Brightness = Brightness.Dark,
        Primary = C(0x0F62FE), Accent = C(0x78A9FF), OnPrimary = C(0xFFFFFF), PrimaryContainer = C(0x002D9C), OnPrimaryContainer = C(0xD0E2FF),
        Secondary = C(0x6F6F6F), OnSecondary = C(0xFFFFFF), SecondaryContainer = C(layer02), OnSecondaryContainer = C(0xF4F4F4),
        Tertiary = C(0xBE95FF), OnTertiary = C(0x161616), TertiaryContainer = C(0x491D8B), OnTertiaryContainer = C(0xE8DAFF),
        Error = C(0xFF8389), OnError = C(0x161616), ErrorContainer = C(0x750E13), OnErrorContainer = C(0xFFD7D9),
        Surface = C(background), OnSurface = C(0xF4F4F4), SurfaceVariant = C(layer01), OnSurfaceVariant = C(0xC6C6C6),
        Outline = C(0x8D8D8D), OutlineVariant = C(layer02),
        Shadow = C(0x000000), Scrim = C(0x161616),
        InverseSurface = C(0xF4F4F4), OnInverseSurface = C(0x161616), InversePrimary = C(0x0F62FE),
        SurfaceDim = C(background), SurfaceBright = C(layer02),
        SurfaceContainerLowest = C(background), SurfaceContainerLow = C(layer01), SurfaceContainer = C(layer01),
        SurfaceContainerHigh = C(layer02), SurfaceContainerHighest = C(layer01),
    };

    /// <summary>Carbon White: a white page with Gray 10 layers.</summary>
    public static readonly ColorScheme White = Light(background: 0xFFFFFF, layer01: 0xF4F4F4, layer02: 0xFFFFFF);

    /// <summary>Carbon Gray 10: a Gray 10 page with white layers.</summary>
    public static readonly ColorScheme Gray10 = Light(background: 0xF4F4F4, layer01: 0xFFFFFF, layer02: 0xF4F4F4);

    /// <summary>Carbon Gray 90.</summary>
    public static readonly ColorScheme Gray90 = Dark(background: 0x262626, layer01: 0x393939, layer02: 0x525252);

    /// <summary>Carbon Gray 100, the darkest theme.</summary>
    public static readonly ColorScheme Gray100 = Dark(background: 0x161616, layer01: 0x262626, layer02: 0x393939);
}

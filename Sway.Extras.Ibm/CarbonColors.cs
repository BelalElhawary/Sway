using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>
/// Carbon's colour tokens for one theme. The names follow Carbon's own token names (layer-01, border-strong-01,
/// text-secondary and so on), so the Carbon docs and design kits map straight onto these.
/// </summary>
public sealed record CarbonColors
{
    public required Brightness Brightness { get; init; }

    // Background and layers. Layers sit on the background: layer-01 on background, layer-02 on layer-01, and so on.
    public required SKColor Background { get; init; }
    public required SKColor BackgroundHover { get; init; }
    public required SKColor BackgroundActive { get; init; }
    public required SKColor BackgroundSelected { get; init; }
    public required SKColor BackgroundInverse { get; init; }
    public required SKColor Layer01 { get; init; }
    public required SKColor Layer02 { get; init; }
    public required SKColor Layer03 { get; init; }
    public required SKColor LayerHover01 { get; init; }
    public required SKColor LayerHover02 { get; init; }
    public required SKColor LayerActive01 { get; init; }
    public required SKColor LayerSelected01 { get; init; }
    public required SKColor LayerSelectedHover01 { get; init; }
    public required SKColor LayerAccent01 { get; init; }
    public required SKColor LayerAccentHover01 { get; init; }

    // Form fields.
    public required SKColor Field01 { get; init; }
    public required SKColor Field02 { get; init; }
    public required SKColor FieldHover01 { get; init; }

    // Borders.
    public required SKColor BorderSubtle00 { get; init; }
    public required SKColor BorderSubtle01 { get; init; }
    public required SKColor BorderStrong01 { get; init; }
    public required SKColor BorderInverse { get; init; }
    public required SKColor BorderInteractive { get; init; }
    public required SKColor BorderDisabled { get; init; }

    // Text, links and icons.
    public required SKColor TextPrimary { get; init; }
    public required SKColor TextSecondary { get; init; }
    public required SKColor TextPlaceholder { get; init; }
    public required SKColor TextHelper { get; init; }
    public required SKColor TextError { get; init; }
    public required SKColor TextInverse { get; init; }
    public required SKColor TextOnColor { get; init; }
    public required SKColor TextOnColorDisabled { get; init; }
    public required SKColor TextDisabled { get; init; }
    public required SKColor LinkPrimary { get; init; }
    public required SKColor LinkInverse { get; init; }
    public required SKColor IconPrimary { get; init; }
    public required SKColor IconSecondary { get; init; }
    public required SKColor IconInverse { get; init; }
    public required SKColor IconOnColor { get; init; }
    public required SKColor IconDisabled { get; init; }

    // Interaction and status.
    public required SKColor Interactive { get; init; }
    public required SKColor Focus { get; init; }
    public required SKColor FocusInverse { get; init; }
    public required SKColor Highlight { get; init; }
    public required SKColor Overlay { get; init; }
    public required SKColor SupportError { get; init; }
    public required SKColor SupportSuccess { get; init; }
    public required SKColor SupportWarning { get; init; }
    public required SKColor SupportInfo { get; init; }

    // Buttons.
    public required SKColor ButtonPrimary { get; init; }
    public required SKColor ButtonPrimaryHover { get; init; }
    public required SKColor ButtonPrimaryActive { get; init; }
    public required SKColor ButtonSecondary { get; init; }
    public required SKColor ButtonSecondaryHover { get; init; }
    public required SKColor ButtonSecondaryActive { get; init; }
    public required SKColor ButtonTertiary { get; init; }
    public required SKColor ButtonTertiaryHover { get; init; }
    public required SKColor ButtonTertiaryActive { get; init; }
    public required SKColor ButtonDangerPrimary { get; init; }
    public required SKColor ButtonDangerHover { get; init; }
    public required SKColor ButtonDangerActive { get; init; }
    public required SKColor ButtonDisabled { get; init; }

    static SKColor C(uint rgb) => Colors.FromRgb(rgb);
    static SKColor A(uint rgb, float opacity) => Colors.FromRgb(rgb).WithOpacity(opacity);

    /// <summary>Carbon White: a white page with Gray 10 layers.</summary>
    public static readonly CarbonColors White = new()
    {
        Brightness = Brightness.Light,
        Background = C(0xFFFFFF), BackgroundHover = C(0xE8E8E8), BackgroundActive = C(0xC6C6C6), BackgroundSelected = C(0xE0E0E0), BackgroundInverse = C(0x393939),
        Layer01 = C(0xF4F4F4), Layer02 = C(0xFFFFFF), Layer03 = C(0xF4F4F4), LayerHover01 = C(0xE8E8E8), LayerHover02 = C(0xE8E8E8), LayerActive01 = C(0xC6C6C6),
        LayerSelected01 = C(0xE0E0E0), LayerSelectedHover01 = C(0xD1D1D1), LayerAccent01 = C(0xE0E0E0), LayerAccentHover01 = C(0xD1D1D1),
        Field01 = C(0xF4F4F4), Field02 = C(0xFFFFFF), FieldHover01 = C(0xE8E8E8),
        BorderSubtle00 = C(0xE0E0E0), BorderSubtle01 = C(0xC6C6C6), BorderStrong01 = C(0x8D8D8D), BorderInverse = C(0x161616), BorderInteractive = C(0x0F62FE), BorderDisabled = C(0xC6C6C6),
        TextPrimary = C(0x161616), TextSecondary = C(0x525252), TextPlaceholder = C(0xA8A8A8), TextHelper = C(0x6F6F6F), TextError = C(0xDA1E28), TextInverse = C(0xFFFFFF),
        TextOnColor = C(0xFFFFFF), TextOnColorDisabled = C(0x8D8D8D), TextDisabled = A(0x161616, 0.25f),
        LinkPrimary = C(0x0F62FE), LinkInverse = C(0x78A9FF),
        IconPrimary = C(0x161616), IconSecondary = C(0x525252), IconInverse = C(0xFFFFFF), IconOnColor = C(0xFFFFFF), IconDisabled = A(0x161616, 0.25f),
        Interactive = C(0x0F62FE), Focus = C(0x0F62FE), FocusInverse = C(0xFFFFFF), Highlight = C(0xD0E2FF), Overlay = A(0x161616, 0.5f),
        SupportError = C(0xDA1E28), SupportSuccess = C(0x198038), SupportWarning = C(0xF1C21B), SupportInfo = C(0x0043CE),
        ButtonPrimary = C(0x0F62FE), ButtonPrimaryHover = C(0x0353E9), ButtonPrimaryActive = C(0x002D9C),
        ButtonSecondary = C(0x393939), ButtonSecondaryHover = C(0x4C4C4C), ButtonSecondaryActive = C(0x6F6F6F),
        ButtonTertiary = C(0x0F62FE), ButtonTertiaryHover = C(0x0353E9), ButtonTertiaryActive = C(0x002D9C),
        ButtonDangerPrimary = C(0xDA1E28), ButtonDangerHover = C(0xB81921), ButtonDangerActive = C(0x750E13), ButtonDisabled = C(0xC6C6C6),
    };

    /// <summary>Carbon Gray 10: a Gray 10 page with white layers.</summary>
    public static readonly CarbonColors Gray10 = White with
    {
        Background = C(0xF4F4F4),
        Layer01 = C(0xFFFFFF), Layer02 = C(0xF4F4F4), Layer03 = C(0xFFFFFF),
        Field01 = C(0xFFFFFF), Field02 = C(0xF4F4F4),
    };

    /// <summary>Carbon Gray 90.</summary>
    public static readonly CarbonColors Gray90 = new()
    {
        Brightness = Brightness.Dark,
        Background = C(0x262626), BackgroundHover = C(0x333333), BackgroundActive = C(0x4C4C4C), BackgroundSelected = C(0x393939), BackgroundInverse = C(0xF4F4F4),
        Layer01 = C(0x393939), Layer02 = C(0x525252), Layer03 = C(0x6F6F6F), LayerHover01 = C(0x474747), LayerHover02 = C(0x636363), LayerActive01 = C(0x6F6F6F),
        LayerSelected01 = C(0x525252), LayerSelectedHover01 = C(0x636363), LayerAccent01 = C(0x525252), LayerAccentHover01 = C(0x636363),
        Field01 = C(0x393939), Field02 = C(0x525252), FieldHover01 = C(0x474747),
        BorderSubtle00 = C(0x525252), BorderSubtle01 = C(0x6F6F6F), BorderStrong01 = C(0xA8A8A8), BorderInverse = C(0xF4F4F4), BorderInteractive = C(0x4589FF), BorderDisabled = C(0x6F6F6F),
        TextPrimary = C(0xF4F4F4), TextSecondary = C(0xC6C6C6), TextPlaceholder = C(0x8D8D8D), TextHelper = C(0xC6C6C6), TextError = C(0xFFB3B8), TextInverse = C(0x161616),
        TextOnColor = C(0xFFFFFF), TextOnColorDisabled = C(0x8D8D8D), TextDisabled = A(0xF4F4F4, 0.25f),
        LinkPrimary = C(0x78A9FF), LinkInverse = C(0x0F62FE),
        IconPrimary = C(0xF4F4F4), IconSecondary = C(0xC6C6C6), IconInverse = C(0x161616), IconOnColor = C(0xFFFFFF), IconDisabled = A(0xF4F4F4, 0.25f),
        Interactive = C(0x4589FF), Focus = C(0xFFFFFF), FocusInverse = C(0x0F62FE), Highlight = C(0x0043CE), Overlay = A(0x161616, 0.7f),
        SupportError = C(0xFF8389), SupportSuccess = C(0x42BE65), SupportWarning = C(0xF1C21B), SupportInfo = C(0x4589FF),
        ButtonPrimary = C(0x0F62FE), ButtonPrimaryHover = C(0x0353E9), ButtonPrimaryActive = C(0x002D9C),
        ButtonSecondary = C(0x6F6F6F), ButtonSecondaryHover = C(0x606060), ButtonSecondaryActive = C(0x393939),
        ButtonTertiary = C(0xFFFFFF), ButtonTertiaryHover = C(0xF4F4F4), ButtonTertiaryActive = C(0xC6C6C6),
        ButtonDangerPrimary = C(0xDA1E28), ButtonDangerHover = C(0xB81921), ButtonDangerActive = C(0x750E13), ButtonDisabled = C(0x6F6F6F),
    };

    /// <summary>Carbon Gray 100, the darkest theme.</summary>
    public static readonly CarbonColors Gray100 = Gray90 with
    {
        Background = C(0x161616), BackgroundHover = C(0x292929), BackgroundActive = C(0x393939), BackgroundSelected = C(0x262626),
        Layer01 = C(0x262626), Layer02 = C(0x393939), Layer03 = C(0x525252), LayerHover01 = C(0x333333), LayerHover02 = C(0x474747), LayerActive01 = C(0x525252),
        LayerSelected01 = C(0x393939), LayerSelectedHover01 = C(0x474747), LayerAccent01 = C(0x393939), LayerAccentHover01 = C(0x474747),
        Field01 = C(0x262626), Field02 = C(0x393939), FieldHover01 = C(0x333333),
        BorderSubtle00 = C(0x393939), BorderSubtle01 = C(0x525252), BorderStrong01 = C(0x6F6F6F), BorderDisabled = C(0x525252),
        ButtonDisabled = C(0x525252),
    };
}

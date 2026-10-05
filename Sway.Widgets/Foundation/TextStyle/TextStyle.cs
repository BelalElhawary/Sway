using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Text appearance. Unset (null) fields are inherited from the enclosing <see cref="DefaultTextStyle"/> via <see cref="Merge"/>.</summary>
public sealed record TextStyle(
    SKColor? Color = null,
    float? FontSize = null,
    int? FontWeight = null,
    bool? Italic = null,
    string? FontFamily = null,
    float? Height = null,
    float? LetterSpacing = null,
    TextDecoration? Decoration = null,
    SKColor? DecorationColor = null,
    IReadOnlyList<BoxShadow>? Shadows = null)
{
    public const float DefaultFontSize = 14;
    public const string DefaultFontFamily = "Roboto";

    /// <summary>Fully-specified style used as the root of every merge.</summary>
    public static readonly TextStyle Fallback = new(
        Color: SKColors.Black, FontSize: DefaultFontSize, FontWeight: Widgets.FontWeight.Normal, Italic: false,
        FontFamily: DefaultFontFamily, Decoration: TextDecoration.None);

    public TextStyle Merge(TextStyle? other) => other is null ? this : new(
        other.Color ?? Color, other.FontSize ?? FontSize, other.FontWeight ?? FontWeight, other.Italic ?? Italic,
        other.FontFamily ?? FontFamily, other.Height ?? Height, other.LetterSpacing ?? LetterSpacing,
        other.Decoration ?? Decoration, other.DecorationColor ?? DecorationColor, other.Shadows ?? Shadows);

    public SKFont ToFont() => FontCache.Get(
        FontFamily ?? DefaultFontFamily, FontSize ?? DefaultFontSize, FontWeight ?? Widgets.FontWeight.Normal, Italic ?? false);
}

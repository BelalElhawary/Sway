using SkiaSharp;

namespace Sway.Widgets;

public static class FontWeight
{
    public const int W100 = 100, W200 = 200, W300 = 300, W400 = 400, W500 = 500, W600 = 600, W700 = 700, W800 = 800, W900 = 900;
    public const int Normal = W400, Bold = W700;
}

[Flags]
public enum TextDecoration { None = 0, Underline = 1, LineThrough = 2, Overline = 4 }

public enum TextAlign { Start, End, Left, Right, Center }

public enum TextOverflow { Clip, Ellipsis, Visible }

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
    public const string DefaultFontFamily = "Segoe UI";

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

/// <summary>A run of text with optional style, which may contain nested spans that inherit and override it.</summary>
public sealed class TextSpan(string? text = null, TextStyle? style = null, IReadOnlyList<TextSpan>? children = null)
{
    public string? Text { get; } = text;
    public TextStyle? Style { get; } = style;
    public IReadOnlyList<TextSpan>? Children { get; } = children;

    public string ToPlainText()
    {
        var sb = new System.Text.StringBuilder();
        void Walk(TextSpan s)
        {
            sb.Append(s.Text);
            if (s.Children is not null) foreach (var c in s.Children) Walk(c);
        }
        Walk(this);
        return sb.ToString();
    }

    public override bool Equals(object? obj) => obj is TextSpan o && Text == o.Text && Equals(Style, o.Style) && ChildrenEqual(o);

    bool ChildrenEqual(TextSpan o)
    {
        if (Children is null || o.Children is null) return Children is null && o.Children is null;
        return Children.SequenceEqual(o.Children);
    }

    public override int GetHashCode() => HashCode.Combine(Text, Style);
}

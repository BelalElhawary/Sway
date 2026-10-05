using SkiaSharp;

namespace Sway.Widgets;

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

using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Alignment(float X, float Y) : IAlignment
{
    public static readonly Alignment TopLeft = new(-1, -1);
    public static readonly Alignment TopCenter = new(0, -1);
    public static readonly Alignment TopRight = new(1, -1);
    public static readonly Alignment CenterLeft = new(-1, 0);
    public static readonly Alignment Center = new(0, 0);
    public static readonly Alignment CenterRight = new(1, 0);
    public static readonly Alignment BottomLeft = new(-1, 1);
    public static readonly Alignment BottomCenter = new(0, 1);
    public static readonly Alignment BottomRight = new(1, 1);

    public Alignment Resolve(TextDirection direction) => this;

    /// <summary>The offset to place a child of <paramref name="child"/> inside <paramref name="parent"/>.</summary>
    public Offset AlongSize(Size parent, Size child) =>
        new((parent.Width - child.Width) / 2 * (1 + X), (parent.Height - child.Height) / 2 * (1 + Y));
}

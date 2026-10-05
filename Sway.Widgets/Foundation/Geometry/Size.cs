using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Size(float Width, float Height)
{
    public static readonly Size Zero = new(0, 0);
    public static Size Square(float side) => new(side, side);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public Rect ToRect(Offset origin = default) => new(origin.Dx, origin.Dy, Width, Height);
}

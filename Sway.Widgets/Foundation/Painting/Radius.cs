using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Radius(float X, float Y)
{
    public static readonly Radius Zero = new(0, 0);
    public static Radius Circular(float r) => new(r, r);
}

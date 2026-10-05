using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Offset(float Dx, float Dy)
{
    public static readonly Offset Zero = new(0, 0);
    public static Offset operator +(Offset a, Offset b) => new(a.Dx + b.Dx, a.Dy + b.Dy);
    public static Offset operator -(Offset a, Offset b) => new(a.Dx - b.Dx, a.Dy - b.Dy);
    public static Offset operator -(Offset a) => new(-a.Dx, -a.Dy);
    public float Distance => MathF.Sqrt(Dx * Dx + Dy * Dy);
}

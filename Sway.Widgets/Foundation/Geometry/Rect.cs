using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Rect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top + Height;
    public Offset TopLeft => new(Left, Top);
    public Size Size => new(Width, Height);
    public bool Contains(Offset p) => p.Dx >= Left && p.Dx < Right && p.Dy >= Top && p.Dy < Bottom;
    public Rect Shift(Offset o) => new(Left + o.Dx, Top + o.Dy, Width, Height);
    public SKRect ToSk() => new(Left, Top, Right, Bottom);
}

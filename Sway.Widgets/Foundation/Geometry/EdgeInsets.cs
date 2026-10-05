using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Insets in physical directions. Use <see cref="EdgeInsetsDirectional"/> for start/end.</summary>
public readonly record struct EdgeInsets(float Left, float Top, float Right, float Bottom)
{
    public static readonly EdgeInsets Zero = new(0, 0, 0, 0);
    public static EdgeInsets All(float v) => new(v, v, v, v);
    public static EdgeInsets Symmetric(float horizontal = 0, float vertical = 0) => new(horizontal, vertical, horizontal, vertical);
    public static EdgeInsets Only(float left = 0, float top = 0, float right = 0, float bottom = 0) => new(left, top, right, bottom);
    public static EdgeInsets Ltrb(float l, float t, float r, float b) => new(l, t, r, b);

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;
    public Size Collapsed => new(Horizontal, Vertical);

    public static EdgeInsets operator +(EdgeInsets a, EdgeInsets b) => new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);
    public static implicit operator EdgeInsets(float all) => All(all);

    public Rect Deflate(Rect r) => new(r.Left + Left, r.Top + Top, Math.Max(0, r.Width - Horizontal), Math.Max(0, r.Height - Vertical));
}

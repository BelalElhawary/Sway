using SkiaSharp;

namespace Sway.Widgets;

public enum TextDirection { Ltr, Rtl }

public enum Axis { Horizontal, Vertical }

public readonly record struct Offset(float Dx, float Dy)
{
    public static readonly Offset Zero = new(0, 0);
    public static Offset operator +(Offset a, Offset b) => new(a.Dx + b.Dx, a.Dy + b.Dy);
    public static Offset operator -(Offset a, Offset b) => new(a.Dx - b.Dx, a.Dy - b.Dy);
    public static Offset operator -(Offset a) => new(-a.Dx, -a.Dy);
    public float Distance => MathF.Sqrt(Dx * Dx + Dy * Dy);
}

public readonly record struct Size(float Width, float Height)
{
    public static readonly Size Zero = new(0, 0);
    public static Size Square(float side) => new(side, side);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public Rect ToRect(Offset origin = default) => new(origin.Dx, origin.Dy, Width, Height);
}

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

/// <summary>Insets relative to the reading direction; resolved against <see cref="TextDirection"/>.</summary>
public readonly record struct EdgeInsetsDirectional(float Start, float Top, float End, float Bottom)
{
    public static EdgeInsetsDirectional Only(float start = 0, float top = 0, float end = 0, float bottom = 0) => new(start, top, end, bottom);
    public EdgeInsets Resolve(TextDirection d) => d == TextDirection.Ltr ? new(Start, Top, End, Bottom) : new(End, Top, Start, Bottom);
}

/// <summary>A point inside a rectangle: (-1,-1) is top-left, (1,1) is bottom-right.</summary>
public interface IAlignment { Alignment Resolve(TextDirection direction); }

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

public readonly record struct AlignmentDirectional(float Start, float Y) : IAlignment
{
    public static readonly AlignmentDirectional TopStart = new(-1, -1);
    public static readonly AlignmentDirectional TopEnd = new(1, -1);
    public static readonly AlignmentDirectional CenterStart = new(-1, 0);
    public static readonly AlignmentDirectional CenterEnd = new(1, 0);
    public static readonly AlignmentDirectional BottomStart = new(-1, 1);
    public static readonly AlignmentDirectional BottomEnd = new(1, 1);

    public Alignment Resolve(TextDirection d) => new(d == TextDirection.Ltr ? Start : -Start, Y);
}

/// <summary>Layout constraints a parent passes to a child box; the child picks a size inside them.</summary>
public readonly record struct BoxConstraints(float MinWidth, float MaxWidth, float MinHeight, float MaxHeight)
{
    public const float Infinity = float.PositiveInfinity;

    public static BoxConstraints Tight(Size s) => new(s.Width, s.Width, s.Height, s.Height);
    public static BoxConstraints Loose(Size s) => new(0, s.Width, 0, s.Height);
    public static BoxConstraints TightFor(float? width = null, float? height = null) =>
        new(width ?? 0, width ?? Infinity, height ?? 0, height ?? Infinity);
    public static BoxConstraints Expand(float? width = null, float? height = null) =>
        new(width ?? Infinity, width ?? Infinity, height ?? Infinity, height ?? Infinity);

    public bool HasBoundedWidth => MaxWidth < Infinity;
    public bool HasBoundedHeight => MaxHeight < Infinity;
    public bool HasTightWidth => MinWidth >= MaxWidth;
    public bool HasTightHeight => MinHeight >= MaxHeight;
    public bool IsTight => HasTightWidth && HasTightHeight;
    public Size Biggest => new(Constrain(MaxWidth, MinWidth, MaxWidth), Constrain(MaxHeight, MinHeight, MaxHeight));
    public Size Smallest => new(MinWidth, MinHeight);

    static float Constrain(float v, float min, float max) => Math.Clamp(v, min, max);
    public float ConstrainWidth(float w = Infinity) => Constrain(w, MinWidth, MaxWidth);
    public float ConstrainHeight(float h = Infinity) => Constrain(h, MinHeight, MaxHeight);
    public Size Constrain(Size s) => new(ConstrainWidth(s.Width), ConstrainHeight(s.Height));

    public BoxConstraints Loosen() => new(0, MaxWidth, 0, MaxHeight);

    public BoxConstraints Deflate(EdgeInsets e)
    {
        float h = e.Horizontal, v = e.Vertical;
        float minW = Math.Max(0, MinWidth - h), minH = Math.Max(0, MinHeight - v);
        return new(minW, Math.Max(minW, MaxWidth - h), minH, Math.Max(minH, MaxHeight - v));
    }

    /// <summary>Tightens these constraints to fit inside <paramref name="c"/>.</summary>
    public BoxConstraints Enforce(BoxConstraints c) => new(
        Math.Clamp(MinWidth, c.MinWidth, c.MaxWidth), Math.Clamp(MaxWidth, c.MinWidth, c.MaxWidth),
        Math.Clamp(MinHeight, c.MinHeight, c.MaxHeight), Math.Clamp(MaxHeight, c.MinHeight, c.MaxHeight));

    public BoxConstraints Tighten(float? width = null, float? height = null) => new(
        width is { } w ? Math.Clamp(w, MinWidth, MaxWidth) : MinWidth,
        width is { } w2 ? Math.Clamp(w2, MinWidth, MaxWidth) : MaxWidth,
        height is { } h ? Math.Clamp(h, MinHeight, MaxHeight) : MinHeight,
        height is { } h2 ? Math.Clamp(h2, MinHeight, MaxHeight) : MaxHeight);

    public BoxConstraints Copy(float? minWidth = null, float? maxWidth = null, float? minHeight = null, float? maxHeight = null) =>
        new(minWidth ?? MinWidth, maxWidth ?? MaxWidth, minHeight ?? MinHeight, maxHeight ?? MaxHeight);
}

public static class Colors
{
    public static readonly SKColor Transparent = SKColors.Transparent;
    public static readonly SKColor Black = SKColors.Black;
    public static readonly SKColor White = SKColors.White;
    public static readonly SKColor Red = new(0xF4, 0x43, 0x36);
    public static readonly SKColor Pink = new(0xE9, 0x1E, 0x63);
    public static readonly SKColor Purple = new(0x9C, 0x27, 0xB0);
    public static readonly SKColor Indigo = new(0x3F, 0x51, 0xB5);
    public static readonly SKColor Blue = new(0x21, 0x96, 0xF3);
    public static readonly SKColor Teal = new(0x00, 0x96, 0x88);
    public static readonly SKColor Green = new(0x4C, 0xAF, 0x50);
    public static readonly SKColor Amber = new(0xFF, 0xC1, 0x07);
    public static readonly SKColor Orange = new(0xFF, 0x98, 0x00);
    public static readonly SKColor Grey = new(0x9E, 0x9E, 0x9E);
    public static readonly SKColor BlueGrey = new(0x60, 0x7D, 0x8B);

    public static SKColor FromRgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    public static SKColor FromArgb(uint argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
    public static SKColor WithOpacity(this SKColor c, float opacity) => c.WithAlpha((byte)Math.Clamp(opacity * 255, 0, 255));
}

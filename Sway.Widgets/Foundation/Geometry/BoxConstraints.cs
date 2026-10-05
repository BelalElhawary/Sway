using SkiaSharp;

namespace Sway.Widgets;

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

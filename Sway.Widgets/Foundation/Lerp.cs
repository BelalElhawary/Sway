using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Interpolation between values of the types used by decorations, styles and layout.</summary>
public static class Lerps
{
    public static float Float(float a, float b, float t) => a + (b - a) * t;

    public static SKColor Color(SKColor a, SKColor b, float t) => new(
        (byte)Math.Clamp(Float(a.Red, b.Red, t), 0, 255), (byte)Math.Clamp(Float(a.Green, b.Green, t), 0, 255),
        (byte)Math.Clamp(Float(a.Blue, b.Blue, t), 0, 255), (byte)Math.Clamp(Float(a.Alpha, b.Alpha, t), 0, 255));

    /// <summary>A missing colour fades in or out as the other colour with its alpha scaled.</summary>
    public static SKColor? Color(SKColor? a, SKColor? b, float t)
    {
        if (a is null && b is null) return null;
        if (a is null) return b!.Value.WithAlpha((byte)(b.Value.Alpha * t));
        if (b is null) return a.Value.WithAlpha((byte)(a.Value.Alpha * (1 - t)));
        return Color(a.Value, b.Value, t);
    }

    public static Offset Offset(Offset a, Offset b, float t) => new(Float(a.Dx, b.Dx, t), Float(a.Dy, b.Dy, t));
    public static Size Size(Size a, Size b, float t) => new(Float(a.Width, b.Width, t), Float(a.Height, b.Height, t));
    public static Rect Rect(Rect a, Rect b, float t) =>
        new(Float(a.Left, b.Left, t), Float(a.Top, b.Top, t), Float(a.Width, b.Width, t), Float(a.Height, b.Height, t));

    public static EdgeInsets EdgeInsets(EdgeInsets a, EdgeInsets b, float t) =>
        new(Float(a.Left, b.Left, t), Float(a.Top, b.Top, t), Float(a.Right, b.Right, t), Float(a.Bottom, b.Bottom, t));

    public static Alignment Alignment(Alignment a, Alignment b, float t) => new(Float(a.X, b.X, t), Float(a.Y, b.Y, t));

    public static BoxConstraints BoxConstraints(BoxConstraints a, BoxConstraints b, float t) => new(
        Finite(a.MinWidth, b.MinWidth, t), Finite(a.MaxWidth, b.MaxWidth, t),
        Finite(a.MinHeight, b.MinHeight, t), Finite(a.MaxHeight, b.MaxHeight, t));

    // Infinity cannot be blended; it holds until the other side is reached.
    static float Finite(float a, float b, float t) =>
        float.IsInfinity(a) || float.IsInfinity(b) ? (t < 0.5f ? a : b) : Float(a, b, t);

    static Radius Radius(Radius a, Radius b, float t) => new(Float(a.X, b.X, t), Float(a.Y, b.Y, t));

    public static BorderRadius BorderRadius(BorderRadius a, BorderRadius b, float t) =>
        new(Radius(a.TopLeft, b.TopLeft, t), Radius(a.TopRight, b.TopRight, t), Radius(a.BottomRight, b.BottomRight, t), Radius(a.BottomLeft, b.BottomLeft, t));

    public static BorderSide BorderSide(BorderSide a, BorderSide b, float t)
    {
        // A side that is absent grows from its own colour so the border does not flash.
        var ca = a.Width == 0 ? b.Color.WithAlpha(0) : a.Color;
        var cb = b.Width == 0 ? a.Color.WithAlpha(0) : b.Color;
        return new(Color(ca, cb, t), Math.Max(0, Float(a.Width, b.Width, t)));
    }

    public static Border Border(Border a, Border b, float t) =>
        new(BorderSide(a.Left, b.Left, t), BorderSide(a.Top, b.Top, t), BorderSide(a.Right, b.Right, t), BorderSide(a.Bottom, b.Bottom, t));

    public static BoxShadow BoxShadow(BoxShadow a, BoxShadow b, float t) => new(
        Color(a.Color, b.Color, t), Offset(a.Offset, b.Offset, t), Float(a.BlurRadius, b.BlurRadius, t), Float(a.SpreadRadius, b.SpreadRadius, t));

    /// <summary>Lists of different length blend the extras in from (or out to) a transparent shadow of the same offset.</summary>
    public static IReadOnlyList<BoxShadow>? BoxShadows(IReadOnlyList<BoxShadow>? a, IReadOnlyList<BoxShadow>? b, float t)
    {
        if (a is null && b is null) return null;
        a ??= Array.Empty<BoxShadow>();
        b ??= Array.Empty<BoxShadow>();
        var result = new List<BoxShadow>();
        for (int i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            var sa = i < a.Count ? a[i] : b[i] with { Color = b[i].Color.WithAlpha(0), BlurRadius = 0, SpreadRadius = 0, Offset = default };
            var sb = i < b.Count ? b[i] : a[i] with { Color = a[i].Color.WithAlpha(0), BlurRadius = 0, SpreadRadius = 0, Offset = default };
            result.Add(BoxShadow(sa, sb, t));
        }
        return result;
    }

    /// <summary>Gradients of the same kind blend (ramps of different lengths are resampled onto shared stops); different kinds switch halfway.</summary>
    public static Gradient? Gradient(Gradient? a, Gradient? b, float t)
    {
        if (a is null && b is null) return null;
        if (a is LinearGradient la && b is LinearGradient lb && la.CanLerp(lb))
            return la.Lerp(lb, t);
        if (a is RadialGradient ra && b is RadialGradient rb && ra.CanLerp(rb))
            return ra.Lerp(rb, t);
        if (a is SweepGradient sa && b is SweepGradient sb && sa.CanLerp(sb))
            return sa.Lerp(sb, t);
        return t < 0.5f ? a : b;
    }

    public static BoxDecoration BoxDecoration(BoxDecoration? a, BoxDecoration? b, float t)
    {
        a ??= new BoxDecoration();
        b ??= new BoxDecoration();
        Border? border = a.Border is null && b.Border is null ? null
            : Border(a.Border ?? Widgets.Border.Only(), b.Border ?? Widgets.Border.Only(), t);
        BorderRadius? radius = a.BorderRadius is null && b.BorderRadius is null ? null
            : BorderRadius(a.BorderRadius ?? Widgets.BorderRadius.Zero, b.BorderRadius ?? Widgets.BorderRadius.Zero, t);
        return new BoxDecoration(
            Color(a.Color, b.Color, t), border, radius, BoxShadows(a.BoxShadow, b.BoxShadow, t),
            Gradient(a.Gradient, b.Gradient, t), t < 0.5f ? a.Shape : b.Shape);
    }

    public static TextStyle TextStyle(TextStyle a, TextStyle b, float t)
    {
        T? Pick<T>(T? x, T? y) => t < 0.5f ? x : y;
        float? F(float? x, float? y) => x is { } fx && y is { } fy ? Float(fx, fy, t) : Pick(x, y);
        return new TextStyle(
            Color(a.Color, b.Color, t),
            F(a.FontSize, b.FontSize),
            a.FontWeight is { } wa && b.FontWeight is { } wb ? (int)(Math.Round(Float(wa, wb, t) / 100) * 100) : Pick(a.FontWeight, b.FontWeight),
            Pick(a.Italic, b.Italic), Pick(a.FontFamily, b.FontFamily), F(a.Height, b.Height), F(a.LetterSpacing, b.LetterSpacing),
            Pick(a.Decoration, b.Decoration), Color(a.DecorationColor, b.DecorationColor, t), Pick(a.Shadows, b.Shadows));
    }

    public static SKMatrix Matrix(SKMatrix a, SKMatrix b, float t) => new(
        Float(a.ScaleX, b.ScaleX, t), Float(a.SkewX, b.SkewX, t), Float(a.TransX, b.TransX, t),
        Float(a.SkewY, b.SkewY, t), Float(a.ScaleY, b.ScaleY, t), Float(a.TransY, b.TransY, t),
        Float(a.Persp0, b.Persp0, t), Float(a.Persp1, b.Persp1, t), Float(a.Persp2, b.Persp2, t));
}

using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct Radius(float X, float Y)
{
    public static readonly Radius Zero = new(0, 0);
    public static Radius Circular(float r) => new(r, r);
}

public readonly record struct BorderRadius(Radius TopLeft, Radius TopRight, Radius BottomRight, Radius BottomLeft)
{
    public static readonly BorderRadius Zero = new(Radius.Zero, Radius.Zero, Radius.Zero, Radius.Zero);
    public static BorderRadius Circular(float r) { var c = Radius.Circular(r); return new(c, c, c, c); }
    public static BorderRadius Only(float topLeft = 0, float topRight = 0, float bottomRight = 0, float bottomLeft = 0) =>
        new(Radius.Circular(topLeft), Radius.Circular(topRight), Radius.Circular(bottomRight), Radius.Circular(bottomLeft));
    public static implicit operator BorderRadius(float r) => Circular(r);

    public bool IsZero => this == Zero;

    public SKRoundRect ToRoundRect(Rect rect)
    {
        var rr = new SKRoundRect();
        rr.SetRectRadii(rect.ToSk(), new[]
        {
            new SKPoint(TopLeft.X, TopLeft.Y), new SKPoint(TopRight.X, TopRight.Y),
            new SKPoint(BottomRight.X, BottomRight.Y), new SKPoint(BottomLeft.X, BottomLeft.Y),
        });
        return rr;
    }

    public BorderRadius Mirrored() => new(TopRight, TopLeft, BottomLeft, BottomRight);
}

public readonly record struct BorderSide(SKColor Color, float Width = 1)
{
    public static readonly BorderSide None = new(SKColors.Transparent, 0);
    public bool IsVisible => Width > 0 && Color.Alpha > 0;
}

public readonly record struct Border(BorderSide Left, BorderSide Top, BorderSide Right, BorderSide Bottom)
{
    public static Border All(SKColor color, float width = 1) { var s = new BorderSide(color, width); return new(s, s, s, s); }
    public static Border Only(BorderSide? left = null, BorderSide? top = null, BorderSide? right = null, BorderSide? bottom = null) =>
        new(left ?? BorderSide.None, top ?? BorderSide.None, right ?? BorderSide.None, bottom ?? BorderSide.None);
    public EdgeInsets Dimensions => new(Left.Width, Top.Width, Right.Width, Bottom.Width);
    public bool IsUniform => Left == Top && Top == Right && Right == Bottom;
}

public readonly record struct BoxShadow(SKColor Color, Offset Offset = default, float BlurRadius = 0, float SpreadRadius = 0);

public enum BoxShape { Rectangle, Circle }

/// <summary>Blends two colour ramps that may have different numbers of stops by sampling both at the union of their stop positions.</summary>
static class GradientMath
{
    static float[] Positions(int count, IReadOnlyList<float>? stops)
    {
        if (stops is not null && stops.Count == count) return stops.ToArray();
        var even = new float[count];
        for (int i = 0; i < count; i++) even[i] = count == 1 ? 0 : i / (float)(count - 1);
        return even;
    }

    static SKColor ColorAt(IReadOnlyList<SKColor> colors, float[] positions, float p)
    {
        if (p <= positions[0]) return colors[0];
        for (int i = 1; i < positions.Length; i++)
        {
            if (p > positions[i]) continue;
            float span = positions[i] - positions[i - 1];
            return span <= 0 ? colors[i] : Lerps.Color(colors[i - 1], colors[i], (p - positions[i - 1]) / span);
        }
        return colors[^1];
    }

    public static (List<SKColor> colors, List<float> stops) Blend(IReadOnlyList<SKColor> a, IReadOnlyList<float>? stopsA,
        IReadOnlyList<SKColor> b, IReadOnlyList<float>? stopsB, float t)
    {
        var pa = Positions(a.Count, stopsA);
        var pb = Positions(b.Count, stopsB);
        var union = pa.Concat(pb).Distinct().OrderBy(p => p).ToList();
        var colors = union.Select(p => Lerps.Color(ColorAt(a, pa, p), ColorAt(b, pb, p), t)).ToList();
        return (colors, union);
    }
}

public abstract class Gradient
{
    public abstract SKShader CreateShader(Rect rect, TextDirection direction);

    protected static SKPoint Point(Rect r, Alignment a) =>
        new(r.Left + r.Width / 2 * (1 + a.X), r.Top + r.Height / 2 * (1 + a.Y));
}

public sealed class LinearGradient(IReadOnlyList<SKColor> colors, IAlignment? begin = null, IAlignment? end = null, IReadOnlyList<float>? stops = null) : Gradient
{
    public IReadOnlyList<SKColor> ColorList { get; } = colors;
    public IAlignment? Begin { get; } = begin;
    public IAlignment? End { get; } = end;
    public IReadOnlyList<float>? Stops { get; } = stops;

    internal bool CanLerp(LinearGradient o) => ColorList.Count > 0 && o.ColorList.Count > 0;

    internal LinearGradient Lerp(LinearGradient o, float t)
    {
        var b = Lerps.Alignment((Begin ?? Alignment.CenterLeft).Resolve(TextDirection.Ltr), (o.Begin ?? Alignment.CenterLeft).Resolve(TextDirection.Ltr), t);
        var e = Lerps.Alignment((End ?? Alignment.CenterRight).Resolve(TextDirection.Ltr), (o.End ?? Alignment.CenterRight).Resolve(TextDirection.Ltr), t);
        var (colors, stops) = GradientMath.Blend(ColorList, Stops, o.ColorList, o.Stops, t);
        return new LinearGradient(colors, b, e, stops);
    }

    public override SKShader CreateShader(Rect rect, TextDirection direction)
    {
        var b = (begin ?? Alignment.CenterLeft).Resolve(direction);
        var e = (end ?? Alignment.CenterRight).Resolve(direction);
        return SKShader.CreateLinearGradient(Point(rect, b), Point(rect, e), colors.ToArray(), stops?.ToArray(), SKShaderTileMode.Clamp);
    }
}

public sealed class RadialGradient(IReadOnlyList<SKColor> colors, IAlignment? center = null, float radius = 0.5f, IReadOnlyList<float>? stops = null) : Gradient
{
    public IReadOnlyList<SKColor> ColorList { get; } = colors;
    public IAlignment? Center { get; } = center;
    public float Radius { get; } = radius;
    public IReadOnlyList<float>? Stops { get; } = stops;

    internal bool CanLerp(RadialGradient o) => ColorList.Count > 0 && o.ColorList.Count > 0;

    internal RadialGradient Lerp(RadialGradient o, float t)
    {
        var (colors, stops) = GradientMath.Blend(ColorList, Stops, o.ColorList, o.Stops, t);
        return new RadialGradient(colors,
            Lerps.Alignment((Center ?? Alignment.Center).Resolve(TextDirection.Ltr), (o.Center ?? Alignment.Center).Resolve(TextDirection.Ltr), t),
            Lerps.Float(Radius, o.Radius, t), stops);
    }

    public override SKShader CreateShader(Rect rect, TextDirection direction)
    {
        var c = (center ?? Alignment.Center).Resolve(direction);
        return SKShader.CreateRadialGradient(Point(rect, c), radius * Math.Min(rect.Width, rect.Height),
            colors.ToArray(), stops?.ToArray(), SKShaderTileMode.Clamp);
    }
}

public sealed class SweepGradient(IReadOnlyList<SKColor> colors, IAlignment? center = null, IReadOnlyList<float>? stops = null) : Gradient
{
    public IReadOnlyList<SKColor> ColorList { get; } = colors;
    public IAlignment? Center { get; } = center;
    public IReadOnlyList<float>? Stops { get; } = stops;

    internal bool CanLerp(SweepGradient o) => ColorList.Count > 0 && o.ColorList.Count > 0;

    internal SweepGradient Lerp(SweepGradient o, float t)
    {
        var (blended, blendedStops) = GradientMath.Blend(ColorList, Stops, o.ColorList, o.Stops, t);
        return new SweepGradient(blended,
            Lerps.Alignment((Center ?? Alignment.Center).Resolve(TextDirection.Ltr), (o.Center ?? Alignment.Center).Resolve(TextDirection.Ltr), t), blendedStops);
    }

    public override SKShader CreateShader(Rect rect, TextDirection direction)
    {
        var c = (center ?? Alignment.Center).Resolve(direction);
        return SKShader.CreateSweepGradient(Point(rect, c), colors.ToArray(), stops?.ToArray());
    }
}

public sealed record BoxDecoration(
    SKColor? Color = null,
    Border? Border = null,
    BorderRadius? BorderRadius = null,
    IReadOnlyList<BoxShadow>? BoxShadow = null,
    Gradient? Gradient = null,
    BoxShape Shape = BoxShape.Rectangle)
{
    public EdgeInsets Padding => Border?.Dimensions ?? EdgeInsets.Zero;

    /// <summary>Paints shadows, fill and border for a box of <paramref name="rect"/>.</summary>
    public void Paint(SKCanvas canvas, Rect rect, TextDirection direction)
    {
        using var path = ShapePath(rect);

        if (BoxShadow is not null)
        {
            foreach (var s in BoxShadow)
            {
                using var paint = new SKPaint { Color = s.Color, IsAntialias = true };
                if (s.BlurRadius > 0) paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s.BlurRadius / 2);
                var r = new Rect(rect.Left + s.Offset.Dx - s.SpreadRadius, rect.Top + s.Offset.Dy - s.SpreadRadius,
                    rect.Width + s.SpreadRadius * 2, rect.Height + s.SpreadRadius * 2);
                using var shadowPath = ShapePath(r);
                canvas.DrawPath(shadowPath, paint);
            }
        }

        if (Gradient is not null)
        {
            using var paint = new SKPaint { IsAntialias = true, Shader = Gradient.CreateShader(rect, direction) };
            canvas.DrawPath(path, paint);
        }
        else if (Color is { Alpha: > 0 } fill)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = fill };
            canvas.DrawPath(path, paint);
        }

        if (Border is { } border) PaintBorder(canvas, rect, border);
    }

    SKPath ShapePath(Rect rect)
    {
        var path = new SKPath();
        if (Shape == BoxShape.Circle)
            path.AddOval(rect.ToSk());
        else if (BorderRadius is { IsZero: false } br)
            path.AddRoundRect(br.ToRoundRect(rect));
        else
            path.AddRect(rect.ToSk());
        return path;
    }

    void PaintBorder(SKCanvas canvas, Rect rect, Border border)
    {
        if (border.IsUniform)
        {
            var side = border.Top;
            if (!side.IsVisible) return;
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = side.Width, Color = side.Color };
            var inset = new Rect(rect.Left + side.Width / 2, rect.Top + side.Width / 2, rect.Width - side.Width, rect.Height - side.Width);
            using var path = new SKPath();
            if (Shape == BoxShape.Circle) path.AddOval(inset.ToSk());
            else if (BorderRadius is { IsZero: false } br) path.AddRoundRect(br.ToRoundRect(inset));
            else path.AddRect(inset.ToSk());
            canvas.DrawPath(path, paint);
            return;
        }

        // Non-uniform borders are drawn as filled bands along each edge.
        void Band(BorderSide s, SKRect r)
        {
            if (!s.IsVisible) return;
            using var p = new SKPaint { Color = s.Color, IsAntialias = true };
            canvas.DrawRect(r, p);
        }
        Band(border.Top, new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + border.Top.Width));
        Band(border.Bottom, new SKRect(rect.Left, rect.Bottom - border.Bottom.Width, rect.Right, rect.Bottom));
        Band(border.Left, new SKRect(rect.Left, rect.Top, rect.Left + border.Left.Width, rect.Bottom));
        Band(border.Right, new SKRect(rect.Right - border.Right.Width, rect.Top, rect.Right, rect.Bottom));
    }
}

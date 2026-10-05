using SkiaSharp;

namespace Sway.Widgets;

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

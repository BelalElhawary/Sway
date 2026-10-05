using SkiaSharp;

namespace Sway.Widgets;

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

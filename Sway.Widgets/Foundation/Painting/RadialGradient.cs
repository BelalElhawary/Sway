using SkiaSharp;

namespace Sway.Widgets;

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

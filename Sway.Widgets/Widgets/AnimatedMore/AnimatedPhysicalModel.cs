using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A clipped, coloured, elevated surface whose colour, elevation and corner radius animate.</summary>
public sealed class AnimatedPhysicalModel(TimeSpan duration, Widget? child = null, SKColor? color = null, float elevation = 0,
    BorderRadius? borderRadius = null, SKColor? shadowColor = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal SKColor? Color => color;
    internal float Elevation => elevation;
    internal BorderRadius Radius => borderRadius ?? BorderRadius.Zero;
    internal SKColor? ShadowColor => shadowColor;
    public override State CreateState() => new AnimatedPhysicalModelState();
}

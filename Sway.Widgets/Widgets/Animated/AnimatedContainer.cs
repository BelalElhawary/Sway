using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedContainer(TimeSpan duration, Widget? child = null, IAlignment? alignment = null, EdgeInsets? padding = null,
    SKColor? color = null, BoxDecoration? decoration = null, BoxDecoration? foregroundDecoration = null, float? width = null,
    float? height = null, BoxConstraints? constraints = null, EdgeInsets? margin = null, SKMatrix? transform = null,
    Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal IAlignment? Alignment => alignment;
    internal EdgeInsets? Padding => padding;
    internal BoxDecoration? Decoration => decoration ?? (color is { } c ? new BoxDecoration(Color: c) : null);
    internal BoxDecoration? Foreground => foregroundDecoration;
    internal BoxConstraints? Constraints =>
        width is null && height is null ? constraints
            : (constraints ?? new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity)).Tighten(width, height);
    internal EdgeInsets? Margin => margin;
    internal SKMatrix? TransformMatrix => transform;

    public override State CreateState() => new AnimatedContainerState();
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A <see cref="Positioned"/> that animates its edges; use inside a <see cref="Stack"/>.</summary>
public sealed class AnimatedPositioned(Widget child, TimeSpan duration, float? left = null, float? top = null, float? right = null,
    float? bottom = null, float? width = null, float? height = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget Child => child;
    internal float? Left => left;
    internal float? Top => top;
    internal float? Right => right;
    internal float? Bottom => bottom;
    internal float? Width => width;
    internal float? Height => height;
    public override State CreateState() => new AnimatedPositionedState();
}

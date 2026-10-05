using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedOpacity(float opacity, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => opacity;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedOpacityState();
}

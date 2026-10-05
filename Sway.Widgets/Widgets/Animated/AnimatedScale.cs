using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedScale(float scale, TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null,
    Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => scale;
    internal Widget? Child => child;
    internal Alignment? Origin => alignment;
    public override State CreateState() => new AnimatedScaleState();
}

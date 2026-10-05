using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedAlign(IAlignment alignment, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal IAlignment Target => alignment;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedAlignState();
}

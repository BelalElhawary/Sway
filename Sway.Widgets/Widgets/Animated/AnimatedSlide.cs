using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Animates a translation expressed as a fraction of the child's own size.</summary>
public sealed class AnimatedSlide(Offset offset, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Offset Target => offset;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedSlideState();
}

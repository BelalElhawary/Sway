using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedPadding(EdgeInsets padding, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal EdgeInsets Target => padding;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedPaddingState();
}

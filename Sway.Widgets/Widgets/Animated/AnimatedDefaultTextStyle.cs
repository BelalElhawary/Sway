using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedDefaultTextStyle(TextStyle style, TimeSpan duration, Widget child, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal TextStyle Style => style;
    internal Widget Child => child;
    public override State CreateState() => new AnimatedDefaultTextStyleState();
}

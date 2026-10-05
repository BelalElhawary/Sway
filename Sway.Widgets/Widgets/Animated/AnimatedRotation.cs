using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Animates rotation in turns (1.0 = a full revolution).</summary>
public sealed class AnimatedRotation(float turns, TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null,
    Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => turns;
    internal Widget? Child => child;
    internal Alignment? Origin => alignment;
    public override State CreateState() => new AnimatedRotationState();
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A <see cref="FractionallySizedBox"/> whose factors and alignment animate.</summary>
public sealed class AnimatedFractionallySizedBox(TimeSpan duration, Widget? child = null, float? widthFactor = null, float? heightFactor = null,
    Alignment? alignment = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal float? WidthFactor => widthFactor;
    internal float? HeightFactor => heightFactor;
    internal Alignment Alignment => alignment ?? Alignment.Center;
    public override State CreateState() => new AnimatedFractionallySizedBoxState();
}

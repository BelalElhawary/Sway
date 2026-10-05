using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Animates from the tween's begin to its end on first build, and to each new end when it changes.</summary>
public sealed class TweenAnimationBuilder<T>(Tween<T> tween, TimeSpan duration, Func<BuildContext, T, Widget?, Widget> builder,
    Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key) where T : notnull
{
    internal Tween<T> Tween => tween;
    internal Func<BuildContext, T, Widget?, Widget> Builder => builder;
    internal Widget? Child => child;
    public override State CreateState() => new TweenAnimationBuilderState<T>();
}

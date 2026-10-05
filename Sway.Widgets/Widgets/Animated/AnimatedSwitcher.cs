using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Cross-fades (or runs a custom transition) between its old and new child when the child's type or key changes.</summary>
public sealed class AnimatedSwitcher(TimeSpan duration, Widget? child = null, Func<Widget, Animation<float>, Widget>? transitionBuilder = null,
    TimeSpan? reverseDuration = null, Curve? switchInCurve = null, Curve? switchOutCurve = null, IAlignment? alignment = null, Key? key = null) : StatefulWidget(key)
{
    internal TimeSpan Duration => duration;
    internal TimeSpan ReverseDuration => reverseDuration ?? duration;
    internal Widget? Child => child;
    internal Func<Widget, Animation<float>, Widget> Transition => transitionBuilder ?? ((c, a) => new FadeTransition(a, c));
    internal Curve InCurve => switchInCurve ?? Curves.Linear;
    internal Curve OutCurve => switchOutCurve ?? Curves.Linear;
    internal IAlignment Alignment => alignment ?? Sway.Widgets.Alignment.Center;
    public override State CreateState() => new AnimatedSwitcherState();
}

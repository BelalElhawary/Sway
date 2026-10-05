using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedPaddingState : ImplicitlyAnimatedWidgetState<AnimatedPadding>
{
    Tween<EdgeInsets>? _padding;
    protected override void ForEachTween(ITweenVisitor v) => _padding = v.VisitValue(_padding, Widget.Target, a => new EdgeInsetsTween(a, a));
    public override Widget Build(BuildContext context) => new Padding(_padding!.Evaluate(Animation), Widget.Child);
}

using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedScaleState : ImplicitlyAnimatedWidgetState<AnimatedScale>
{
    Tween<float>? _scale;
    protected override void ForEachTween(ITweenVisitor v) => _scale = v.VisitValue(_scale, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => Transform.Scale(_scale!.Evaluate(Animation), Widget.Child, Widget.Origin);
}

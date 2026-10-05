using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedOpacityState : ImplicitlyAnimatedWidgetState<AnimatedOpacity>
{
    Tween<float>? _opacity;
    protected override void ForEachTween(ITweenVisitor v) => _opacity = v.VisitValue(_opacity, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => new Opacity(_opacity!.Evaluate(Animation), Widget.Child);
}

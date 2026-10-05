using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedAlignState : ImplicitlyAnimatedWidgetState<AnimatedAlign>
{
    Tween<Alignment>? _alignment;
    protected override void ForEachTween(ITweenVisitor v) =>
        _alignment = v.VisitValue(_alignment, (Alignment?)Widget.Target.Resolve(Directionality.Of(Context)), a => new AlignmentTween(a, a));
    public override Widget Build(BuildContext context) => new Align(_alignment!.Evaluate(Animation), Widget.Child);
}

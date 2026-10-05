using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedSlideState : ImplicitlyAnimatedWidgetState<AnimatedSlide>
{
    Tween<Offset>? _offset;
    protected override void ForEachTween(ITweenVisitor v) => _offset = v.VisitValue(_offset, Widget.Target, a => new OffsetTween(a, a));
    public override Widget Build(BuildContext context) => new FractionalTranslation(_offset!.Evaluate(Animation), Widget.Child);
}

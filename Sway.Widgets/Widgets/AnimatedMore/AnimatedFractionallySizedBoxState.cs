using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedFractionallySizedBoxState : ImplicitlyAnimatedWidgetState<AnimatedFractionallySizedBox>
{
    Tween<float>? _width, _height;
    Tween<Alignment>? _alignment;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _width = v.VisitValue(_width, Widget.WidthFactor, a => new FloatTween(a, a));
        _height = v.VisitValue(_height, Widget.HeightFactor, a => new FloatTween(a, a));
        _alignment = v.VisitValue(_alignment, (Alignment?)Widget.Alignment, a => new AlignmentTween(a, a));
    }

    public override Widget Build(BuildContext context) =>
        new FractionallySizedBox(_width?.Evaluate(Animation), _height?.Evaluate(Animation), _alignment!.Evaluate(Animation), Widget.Child);
}

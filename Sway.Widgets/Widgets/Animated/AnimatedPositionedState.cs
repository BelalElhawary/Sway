using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedPositionedState : ImplicitlyAnimatedWidgetState<AnimatedPositioned>
{
    Tween<float>? _left, _top, _right, _bottom, _width, _height;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _left = v.VisitValue(_left, Widget.Left, a => new FloatTween(a, a));
        _top = v.VisitValue(_top, Widget.Top, a => new FloatTween(a, a));
        _right = v.VisitValue(_right, Widget.Right, a => new FloatTween(a, a));
        _bottom = v.VisitValue(_bottom, Widget.Bottom, a => new FloatTween(a, a));
        _width = v.VisitValue(_width, Widget.Width, a => new FloatTween(a, a));
        _height = v.VisitValue(_height, Widget.Height, a => new FloatTween(a, a));
    }

    float? Eval(Tween<float>? t) => t?.Evaluate(Animation);

    public override Widget Build(BuildContext context) =>
        new Positioned(Widget.Child, Eval(_left), Eval(_top), Eval(_right), Eval(_bottom), Eval(_width), Eval(_height));
}

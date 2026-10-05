using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedContainerState : ImplicitlyAnimatedWidgetState<AnimatedContainer>
{
    Tween<Alignment>? _alignment;
    Tween<EdgeInsets>? _padding, _margin;
    Tween<BoxDecoration>? _decoration, _foreground;
    Tween<BoxConstraints>? _constraints;
    Tween<SKMatrix>? _transform;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _alignment = v.VisitValue(_alignment, Widget.Alignment?.Resolve(Directionality.Of(Context)), a => new AlignmentTween(a, a));
        _padding = v.VisitValue(_padding, Widget.Padding, a => new EdgeInsetsTween(a, a));
        _decoration = v.Visit(_decoration, Widget.Decoration, a => new DecorationTween(a, a));
        _foreground = v.Visit(_foreground, Widget.Foreground, a => new DecorationTween(a, a));
        _constraints = v.VisitValue(_constraints, Widget.Constraints, a => new BoxConstraintsTween(a, a));
        _margin = v.VisitValue(_margin, Widget.Margin, a => new EdgeInsetsTween(a, a));
        _transform = v.VisitValue(_transform, Widget.TransformMatrix, a => new MatrixTween(a, a));
    }

    public override Widget Build(BuildContext context) => new Container(
        Widget.Child,
        alignment: _alignment?.Evaluate(Animation),
        padding: _padding?.Evaluate(Animation),
        decoration: _decoration?.Evaluate(Animation),
        foregroundDecoration: _foreground?.Evaluate(Animation),
        constraints: _constraints?.Evaluate(Animation),
        margin: _margin?.Evaluate(Animation),
        transform: _transform?.Evaluate(Animation));
}

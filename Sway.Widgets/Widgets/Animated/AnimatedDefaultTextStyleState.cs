using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedDefaultTextStyleState : ImplicitlyAnimatedWidgetState<AnimatedDefaultTextStyle>
{
    Tween<TextStyle>? _style;
    protected override void ForEachTween(ITweenVisitor v) => _style = v.Visit(_style, Widget.Style, a => new TextStyleTween(a, a));
    public override Widget Build(BuildContext context) => DefaultTextStyle.Merge(context, _style!.Evaluate(Animation), Widget.Child);
}

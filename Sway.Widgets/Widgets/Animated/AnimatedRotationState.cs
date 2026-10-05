using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedRotationState : ImplicitlyAnimatedWidgetState<AnimatedRotation>
{
    Tween<float>? _turns;
    protected override void ForEachTween(ITweenVisitor v) => _turns = v.VisitValue(_turns, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => Transform.Rotate(_turns!.Evaluate(Animation) * MathF.PI * 2, Widget.Child, Widget.Origin);
}

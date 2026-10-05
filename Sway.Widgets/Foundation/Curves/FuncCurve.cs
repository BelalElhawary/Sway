namespace Sway.Widgets;

sealed class FuncCurve(Func<float, float> f) : Curve
{
    protected override float TransformInternal(float t) => f(t);
}

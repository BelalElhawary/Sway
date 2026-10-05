namespace Sway.Widgets;

sealed class FlippedCurve(Curve curve) : Curve
{
    protected override float TransformInternal(float t) => 1 - curve.Transform(1 - t);
}

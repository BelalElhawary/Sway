namespace Sway.Widgets;

public sealed class Threshold(float threshold) : Curve
{
    protected override float TransformInternal(float t) => t < threshold ? 0 : 1;
}

namespace Sway.Widgets;

/// <summary>Holds the first and last values and jumps between them in the given number of steps.</summary>
public sealed class StepsCurve(int steps) : Curve
{
    protected override float TransformInternal(float t) => MathF.Floor(t * steps) / steps;
}

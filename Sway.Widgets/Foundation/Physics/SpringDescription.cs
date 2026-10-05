namespace Sway.Widgets;

/// <summary>Mass, stiffness and damping of a spring.</summary>
public readonly record struct SpringDescription(float Mass, float Stiffness, float Damping)
{
    /// <summary>A spring whose damping is a fraction of critical: 1 settles without overshoot, below 1 bounces.</summary>
    public static SpringDescription WithDampingRatio(float mass, float stiffness, float ratio = 1) =>
        new(mass, stiffness, ratio * 2 * MathF.Sqrt(mass * stiffness));
}

namespace Sway.Widgets;

/// <summary>A position that evolves with time (in seconds) under some physical rule.</summary>
public abstract class Simulation
{
    public Tolerance Tolerance { get; init; } = Tolerance.Default;

    public abstract float X(float time);
    public abstract float Dx(float time);
    public abstract bool IsDone(float time);
}

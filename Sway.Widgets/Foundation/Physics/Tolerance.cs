namespace Sway.Widgets;

/// <summary>How close to rest a simulation must be before it reports done.</summary>
public readonly record struct Tolerance(float Distance, float Velocity)
{
    public static readonly Tolerance Default = new(0.001f, 0.01f);
}

namespace Sway.Widgets;

/// <summary>How close to rest a simulation must be before it reports done.</summary>
public readonly record struct Tolerance(float Distance, float Velocity)
{
    public static readonly Tolerance Default = new(0.001f, 0.01f);
}

/// <summary>A position that evolves with time (in seconds) under some physical rule.</summary>
public abstract class Simulation
{
    public Tolerance Tolerance { get; init; } = Tolerance.Default;

    public abstract float X(float time);
    public abstract float Dx(float time);
    public abstract bool IsDone(float time);
}

/// <summary>Slides from a start position with a velocity that decays by <c>drag</c> each second (0 &lt; drag &lt; 1).</summary>
public sealed class FrictionSimulation : Simulation
{
    readonly float _drag, _logDrag, _position, _velocity;

    public FrictionSimulation(float drag, float position, float velocity)
    {
        if (drag is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(drag), "Drag must be between 0 and 1 exclusive.");
        _drag = drag;
        _logDrag = MathF.Log(drag);
        _position = position;
        _velocity = velocity;
    }

    /// <summary>Where the slide comes to rest.</summary>
    public float FinalX => _position - _velocity / _logDrag;

    public override float X(float time) => _position + _velocity * (MathF.Pow(_drag, time) - 1) / _logDrag;
    public override float Dx(float time) => _velocity * MathF.Pow(_drag, time);
    public override bool IsDone(float time) => MathF.Abs(Dx(time)) < Tolerance.Velocity;
}

/// <summary>Mass, stiffness and damping of a spring.</summary>
public readonly record struct SpringDescription(float Mass, float Stiffness, float Damping)
{
    /// <summary>A spring whose damping is a fraction of critical: 1 settles without overshoot, below 1 bounces.</summary>
    public static SpringDescription WithDampingRatio(float mass, float stiffness, float ratio = 1) =>
        new(mass, stiffness, ratio * 2 * MathF.Sqrt(mass * stiffness));
}

/// <summary>A damped spring pulling a value toward <c>end</c>.</summary>
public sealed class SpringSimulation : Simulation
{
    readonly float _end;
    readonly Func<float, (float x, float dx)> _solution;

    public SpringSimulation(SpringDescription spring, float start, float end, float velocity)
    {
        _end = end;
        float m = spring.Mass, c = spring.Damping, k = spring.Stiffness;
        float x0 = start - end;
        float discriminant = c * c - 4 * m * k;

        if (MathF.Abs(discriminant) < 1e-6f * Math.Max(1, c * c))
        {
            // Critically damped: fastest return without overshoot.
            float r = -c / (2 * m), c1 = x0, c2 = velocity - r * x0;
            _solution = t =>
            {
                float e = MathF.Exp(r * t);
                return ((c1 + c2 * t) * e, (c2 + r * (c1 + c2 * t)) * e);
            };
        }
        else if (discriminant > 0)
        {
            // Overdamped: two decaying exponentials.
            float root = MathF.Sqrt(discriminant);
            float r1 = (-c + root) / (2 * m), r2 = (-c - root) / (2 * m);
            float c2 = (velocity - r1 * x0) / (r2 - r1), c1 = x0 - c2;
            _solution = t =>
            {
                float e1 = MathF.Exp(r1 * t), e2 = MathF.Exp(r2 * t);
                return (c1 * e1 + c2 * e2, c1 * r1 * e1 + c2 * r2 * e2);
            };
        }
        else
        {
            // Underdamped: decaying oscillation.
            float w = MathF.Sqrt(-discriminant) / (2 * m), r = -c / (2 * m);
            float c1 = x0, c2 = (velocity - r * x0) / w;
            _solution = t =>
            {
                float e = MathF.Exp(r * t), cos = MathF.Cos(w * t), sin = MathF.Sin(w * t);
                float wave = c1 * cos + c2 * sin;
                return (e * wave, e * (r * wave + w * (c2 * cos - c1 * sin)));
            };
        }
    }

    public override float X(float time) => _end + _solution(time).x;
    public override float Dx(float time) => _solution(time).dx;

    public override bool IsDone(float time)
    {
        var (x, dx) = _solution(time);
        return MathF.Abs(x) < Tolerance.Distance && MathF.Abs(dx) < Tolerance.Velocity;
    }
}

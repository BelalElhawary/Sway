namespace Sway.Widgets;

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

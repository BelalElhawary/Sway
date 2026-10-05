namespace Sway.Widgets;

/// <summary>A cubic Bezier easing from (0,0) to (1,1) with control points (a,b) and (c,d).</summary>
public sealed class Cubic(float a, float b, float c, float d) : Curve
{
    const float Tolerance = 1e-5f;
    const int MaxIterations = 40;

    static float Sample(float p1, float p2, float t)
    {
        float k = 1 - t;
        return 3 * k * k * t * p1 + 3 * k * t * t * p2 + t * t * t;
    }

    protected override float TransformInternal(float x)
    {
        float lo = 0, hi = 1;
        float mid = 0.5f;
        for (int i = 0; i < MaxIterations; i++)
        {
            mid = (lo + hi) / 2;
            float est = Sample(a, c, mid);
            if (MathF.Abs(x - est) < Tolerance) break;
            if (est < x) lo = mid; else hi = mid;
        }
        return Sample(b, d, mid);
    }
}

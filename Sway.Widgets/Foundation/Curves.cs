namespace Sway.Widgets;

/// <summary>Maps linear progress 0..1 to eased progress.</summary>
public abstract class Curve
{
    public float Transform(float t) => t is 0 or 1 ? t : TransformInternal(t);
    protected abstract float TransformInternal(float t);
    public Curve Flipped => new FlippedCurve(this);
}

sealed class FlippedCurve(Curve curve) : Curve
{
    protected override float TransformInternal(float t) => 1 - curve.Transform(1 - t);
}

sealed class FuncCurve(Func<float, float> f) : Curve
{
    protected override float TransformInternal(float t) => f(t);
}

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

/// <summary>Holds the first and last values and jumps between them in the given number of steps.</summary>
public sealed class StepsCurve(int steps) : Curve
{
    protected override float TransformInternal(float t) => MathF.Floor(t * steps) / steps;
}

/// <summary>Runs <paramref name="curve"/> only between <paramref name="begin"/> and <paramref name="end"/> (both 0..1); used for staggering.</summary>
public sealed class Interval(float begin, float end, Curve? curve = null) : Curve
{
    protected override float TransformInternal(float t)
    {
        t = Math.Clamp((t - begin) / (end - begin), 0, 1);
        return t is 0 or 1 ? t : (curve ?? Curves.Linear).Transform(t);
    }
}

public sealed class Threshold(float threshold) : Curve
{
    protected override float TransformInternal(float t) => t < threshold ? 0 : 1;
}

public static class Curves
{
    public static readonly Curve Linear = new FuncCurve(t => t);
    public static readonly Curve Decelerate = new FuncCurve(t => 1 - (1 - t) * (1 - t));
    public static readonly Curve Ease = new Cubic(0.25f, 0.1f, 0.25f, 1f);
    public static readonly Curve EaseIn = new Cubic(0.42f, 0f, 1f, 1f);
    public static readonly Curve EaseOut = new Cubic(0f, 0f, 0.58f, 1f);
    public static readonly Curve EaseInOut = new Cubic(0.42f, 0f, 0.58f, 1f);
    public static readonly Curve EaseInQuad = new Cubic(0.55f, 0.085f, 0.68f, 0.53f);
    public static readonly Curve EaseOutQuad = new Cubic(0.25f, 0.46f, 0.45f, 0.94f);
    public static readonly Curve EaseInOutQuad = new Cubic(0.455f, 0.03f, 0.515f, 0.955f);
    public static readonly Curve EaseInCubic = new Cubic(0.55f, 0.055f, 0.675f, 0.19f);
    public static readonly Curve EaseOutCubic = new Cubic(0.215f, 0.61f, 0.355f, 1f);
    public static readonly Curve EaseInOutCubic = new Cubic(0.645f, 0.045f, 0.355f, 1f);
    public static readonly Curve EaseInExpo = new Cubic(0.95f, 0.05f, 0.795f, 0.035f);
    public static readonly Curve EaseOutExpo = new Cubic(0.19f, 1f, 0.22f, 1f);
    public static readonly Curve EaseInBack = new Cubic(0.6f, -0.28f, 0.735f, 0.045f);
    public static readonly Curve EaseOutBack = new Cubic(0.175f, 0.885f, 0.32f, 1.275f);
    public static readonly Curve FastOutSlowIn = new Cubic(0.4f, 0f, 0.2f, 1f);

    static float BounceOutF(float t)
    {
        if (t < 1 / 2.75f) return 7.5625f * t * t;
        if (t < 2 / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
        if (t < 2.5f / 2.75f) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
        t -= 2.625f / 2.75f;
        return 7.5625f * t * t + 0.984375f;
    }

    public static readonly Curve BounceOut = new FuncCurve(BounceOutF);
    public static readonly Curve BounceIn = new FuncCurve(t => 1 - BounceOutF(1 - t));
    public static readonly Curve BounceInOut = new FuncCurve(t => t < 0.5f ? (1 - BounceOutF(1 - t * 2)) * 0.5f : BounceOutF(t * 2 - 1) * 0.5f + 0.5f);

    public static readonly Curve ElasticOut = new FuncCurve(t =>
    {
        const float period = 0.4f;
        return MathF.Pow(2, -10 * t) * MathF.Sin((t - period / 4) * (MathF.PI * 2) / period) + 1;
    });
    public static readonly Curve ElasticIn = new FuncCurve(t =>
    {
        const float period = 0.4f;
        t -= 1;
        return -MathF.Pow(2, 10 * t) * MathF.Sin((t - period / 4) * (MathF.PI * 2) / period);
    });
}

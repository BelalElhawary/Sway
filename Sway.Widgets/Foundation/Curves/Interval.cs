namespace Sway.Widgets;

/// <summary>Runs <paramref name="curve"/> only between <paramref name="begin"/> and <paramref name="end"/> (both 0..1); used for staggering.</summary>
public sealed class Interval(float begin, float end, Curve? curve = null) : Curve
{
    protected override float TransformInternal(float t)
    {
        t = Math.Clamp((t - begin) / (end - begin), 0, 1);
        return t is 0 or 1 ? t : (curve ?? Curves.Linear).Transform(t);
    }
}

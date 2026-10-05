namespace Sway.Widgets;

/// <summary>Maps linear progress 0..1 to eased progress.</summary>
public abstract class Curve
{
    public float Transform(float t) => t is 0 or 1 ? t : TransformInternal(t);
    protected abstract float TransformInternal(float t);
    public Curve Flipped => new FlippedCurve(this);
}

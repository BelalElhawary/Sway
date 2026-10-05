using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Something that can be evaluated at progress t (0..1): a <see cref="Tween{T}"/> or a <see cref="TweenSequence{T}"/>.</summary>
public abstract class Animatable<T> where T : notnull
{
    public abstract T Transform(float t);
    public T Evaluate(Animation<float> animation) => Transform(animation.Value);

    public Animation<T> Animate(Animation<float> parent) => new AnimatedEvaluation<T>(parent, this);

    /// <summary>Applies <paramref name="curve"/> to the progress before this animatable sees it.</summary>
    public Animatable<T> Curved(Curve curve) => new CurvedAnimatable<T>(this, curve);
}

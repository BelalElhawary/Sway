using SkiaSharp;

namespace Sway.Widgets;

sealed class CurvedAnimatable<T>(Animatable<T> inner, Curve curve) : Animatable<T> where T : notnull
{
    public override T Transform(float t) => inner.Transform(curve.Transform(t));
}

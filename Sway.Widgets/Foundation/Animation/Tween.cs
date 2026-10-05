using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Interpolates between <see cref="Begin"/> and <see cref="End"/> with a type-specific blend function.</summary>
public class Tween<T>(T begin, T end, Func<T, T, float, T> lerp) : Animatable<T> where T : notnull
{
    public T Begin { get; set; } = begin;
    public T End { get; set; } = end;

    public override T Transform(float t) => t == 0 ? Begin : t == 1 ? End : lerp(Begin, End, t);

    /// <summary>Blends between arbitrary endpoints using this tween's interpolation, without touching its own Begin and End.</summary>
    public T Interpolate(T from, T to, float t) => t == 0 ? from : t == 1 ? to : lerp(from, to, t);
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Insets relative to the reading direction; resolved against <see cref="TextDirection"/>.</summary>
public readonly record struct EdgeInsetsDirectional(float Start, float Top, float End, float Bottom)
{
    public static EdgeInsetsDirectional Only(float start = 0, float top = 0, float end = 0, float bottom = 0) => new(start, top, end, bottom);
    public EdgeInsets Resolve(TextDirection d) => d == TextDirection.Ltr ? new(Start, Top, End, Bottom) : new(End, Top, Start, Bottom);
}

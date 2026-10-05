using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Material 3 elevation: the two-layer shadow for each level (0 to 5).</summary>
public static class Elevation
{
    public static IReadOnlyList<BoxShadow>? Shadows(int level, SKColor shadow)
    {
        if (level <= 0) return null;
        var umbra = shadow.WithOpacity(0.30f);
        var penumbra = shadow.WithOpacity(0.15f);
        return level switch
        {
            1 => [new BoxShadow(umbra, new Offset(0, 1), 2), new BoxShadow(penumbra, new Offset(0, 1), 3, 1)],
            2 => [new BoxShadow(umbra, new Offset(0, 1), 2), new BoxShadow(penumbra, new Offset(0, 2), 6, 2)],
            3 => [new BoxShadow(umbra, new Offset(0, 1), 3), new BoxShadow(penumbra, new Offset(0, 4), 8, 3)],
            4 => [new BoxShadow(umbra, new Offset(0, 2), 3), new BoxShadow(penumbra, new Offset(0, 6), 10, 4)],
            _ => [new BoxShadow(umbra, new Offset(0, 4), 4), new BoxShadow(penumbra, new Offset(0, 8), 12, 6)],
        };
    }
}

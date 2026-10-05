using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct AlignmentDirectional(float Start, float Y) : IAlignment
{
    public static readonly AlignmentDirectional TopStart = new(-1, -1);
    public static readonly AlignmentDirectional TopEnd = new(1, -1);
    public static readonly AlignmentDirectional CenterStart = new(-1, 0);
    public static readonly AlignmentDirectional CenterEnd = new(1, 0);
    public static readonly AlignmentDirectional BottomStart = new(-1, 1);
    public static readonly AlignmentDirectional BottomEnd = new(1, 1);

    public Alignment Resolve(TextDirection d) => new(d == TextDirection.Ltr ? Start : -Start, Y);
}

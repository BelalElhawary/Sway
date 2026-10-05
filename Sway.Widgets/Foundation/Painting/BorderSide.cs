using SkiaSharp;

namespace Sway.Widgets;

public readonly record struct BorderSide(SKColor Color, float Width = 1)
{
    public static readonly BorderSide None = new(SKColors.Transparent, 0);
    public bool IsVisible => Width > 0 && Color.Alpha > 0;
}

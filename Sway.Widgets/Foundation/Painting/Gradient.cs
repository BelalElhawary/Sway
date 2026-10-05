using SkiaSharp;

namespace Sway.Widgets;

public abstract class Gradient
{
    public abstract SKShader CreateShader(Rect rect, TextDirection direction);

    protected static SKPoint Point(Rect r, Alignment a) =>
        new(r.Left + r.Width / 2 * (1 + a.X), r.Top + r.Height / 2 * (1 + a.Y));
}

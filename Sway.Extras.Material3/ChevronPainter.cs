using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

sealed class ChevronPainter(SKColor color, bool up = false) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        float cx = size.Width / 2, cy = size.Height / 2, w = size.Width * 0.28f, h = size.Height * 0.14f;
        using var path = new SKPath();
        float s = up ? -1 : 1;
        path.MoveTo(cx - w, cy - h * s);
        path.LineTo(cx, cy + h * s);
        path.LineTo(cx + w, cy - h * s);
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

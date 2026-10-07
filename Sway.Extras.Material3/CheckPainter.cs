using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

sealed class CheckPainter(SKColor color, float strokeWidth = 2) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        using var b = new SKPathBuilder();
        b.MoveTo(size.Width * 0.20f, size.Height * 0.52f);
        b.LineTo(size.Width * 0.42f, size.Height * 0.74f);
        b.LineTo(size.Width * 0.80f, size.Height * 0.28f);
        using var path = b.Detach();
        canvas.DrawPath(path, p);
    }
    public override bool ShouldRepaint(CustomPainter old) => true;
}

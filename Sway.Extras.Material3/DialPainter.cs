using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Draws the time picker dial: the round face and the hand pointing at the selected value.</summary>
sealed class DialPainter(float selectedDegrees, SKColor face, SKColor hand, float labelRadius, bool showDot) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        float cx = size.Width / 2, cy = size.Height / 2;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = face };
        canvas.DrawCircle(cx, cy, size.Width / 2, paint);

        float rad = (selectedDegrees - 90) * MathF.PI / 180;
        float x = cx + MathF.Cos(rad) * labelRadius, y = cy + MathF.Sin(rad) * labelRadius;
        paint.Color = hand;
        canvas.DrawCircle(cx, cy, 4, paint);
        canvas.DrawCircle(x, y, 20, paint);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        canvas.DrawLine(cx, cy, x, y, paint);
        if (showDot)
        {
            // A minute that is not a multiple of five has no label under the hand, so mark it with a dot.
            paint.Style = SKPaintStyle.Fill;
            paint.Color = SKColors.White.WithAlpha(255);
            canvas.DrawCircle(x, y, 3, paint);
        }
    }

    public override bool ShouldRepaint(CustomPainter old) => true;
}

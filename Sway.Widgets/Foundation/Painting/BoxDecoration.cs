using SkiaSharp;

namespace Sway.Widgets;

public sealed record BoxDecoration(
    SKColor? Color = null,
    Border? Border = null,
    BorderRadius? BorderRadius = null,
    IReadOnlyList<BoxShadow>? BoxShadow = null,
    Gradient? Gradient = null,
    BoxShape Shape = BoxShape.Rectangle)
{
    public EdgeInsets Padding => Border?.Dimensions ?? EdgeInsets.Zero;

    /// <summary>Paints shadows, fill and border for a box of <paramref name="rect"/>.</summary>
    public void Paint(SKCanvas canvas, Rect rect, TextDirection direction)
    {
        using var path = ShapePath(rect);

        if (BoxShadow is not null)
        {
            foreach (var s in BoxShadow)
            {
                using var paint = new SKPaint { Color = s.Color, IsAntialias = true };
                if (s.BlurRadius > 0) paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s.BlurRadius / 2);
                var r = new Rect(rect.Left + s.Offset.Dx - s.SpreadRadius, rect.Top + s.Offset.Dy - s.SpreadRadius,
                    rect.Width + s.SpreadRadius * 2, rect.Height + s.SpreadRadius * 2);
                using var shadowPath = ShapePath(r);
                canvas.DrawPath(shadowPath, paint);
            }
        }

        if (Gradient is not null)
        {
            using var paint = new SKPaint { IsAntialias = true, Shader = Gradient.CreateShader(rect, direction) };
            canvas.DrawPath(path, paint);
        }
        else if (Color is { Alpha: > 0 } fill)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = fill };
            canvas.DrawPath(path, paint);
        }

        if (Border is { } border) PaintBorder(canvas, rect, border);
    }

    SKPath ShapePath(Rect rect)
    {
        var path = new SKPath();
        if (Shape == BoxShape.Circle)
            path.AddOval(rect.ToSk());
        else if (BorderRadius is { IsZero: false } br)
            path.AddRoundRect(br.ToRoundRect(rect));
        else
            path.AddRect(rect.ToSk());
        return path;
    }

    void PaintBorder(SKCanvas canvas, Rect rect, Border border)
    {
        if (border.IsUniform)
        {
            var side = border.Top;
            if (!side.IsVisible) return;
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = side.Width, Color = side.Color };
            var inset = new Rect(rect.Left + side.Width / 2, rect.Top + side.Width / 2, rect.Width - side.Width, rect.Height - side.Width);
            using var path = new SKPath();
            if (Shape == BoxShape.Circle) path.AddOval(inset.ToSk());
            else if (BorderRadius is { IsZero: false } br) path.AddRoundRect(br.ToRoundRect(inset));
            else path.AddRect(inset.ToSk());
            canvas.DrawPath(path, paint);
            return;
        }

        // Non-uniform borders are drawn as filled bands along each edge.
        void Band(BorderSide s, SKRect r)
        {
            if (!s.IsVisible) return;
            using var p = new SKPaint { Color = s.Color, IsAntialias = true };
            canvas.DrawRect(r, p);
        }
        Band(border.Top, new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + border.Top.Width));
        Band(border.Bottom, new SKRect(rect.Left, rect.Bottom - border.Bottom.Width, rect.Right, rect.Bottom));
        Band(border.Left, new SKRect(rect.Left, rect.Top, rect.Left + border.Left.Width, rect.Bottom));
        Band(border.Right, new SKRect(rect.Right - border.Right.Width, rect.Top, rect.Right, rect.Bottom));
    }
}

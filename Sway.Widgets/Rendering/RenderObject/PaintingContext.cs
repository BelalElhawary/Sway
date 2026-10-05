using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Draws into a canvas and composes the clip/opacity/transform effects that wrap child painting.</summary>
public sealed class PaintingContext(SKCanvas canvas)
{
    public SKCanvas Canvas { get; } = canvas;

    // Overflow a child may legitimately paint outside its box (shadows, glows, transforms) before it is culled.
    const float CullMargin = 96;

    /// <summary>Paints <paramref name="child"/>, skipping it when it lies entirely outside the current clip.</summary>
    public void PaintChild(RenderBox child, Offset offset)
    {
        if (child.SizeOrNull is { } s &&
            Canvas.QuickReject(new SKRect(offset.Dx - CullMargin, offset.Dy - CullMargin, offset.Dx + s.Width + CullMargin, offset.Dy + s.Height + CullMargin)))
            return;
        child.Paint(this, offset);
    }

    public void PushClipRect(Offset offset, Rect rect, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.ClipRect(rect.Shift(offset).ToSk(), antialias: true);
        painter(this, offset);
        Canvas.Restore();
    }

    public void PushClipRRect(Offset offset, Rect rect, BorderRadius radius, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.ClipRoundRect(radius.ToRoundRect(rect.Shift(offset)), antialias: true);
        painter(this, offset);
        Canvas.Restore();
    }

    public void PushOpacity(Offset offset, float opacity, Action<PaintingContext, Offset> painter)
    {
        if (opacity <= 0) return;
        if (opacity >= 1) { painter(this, offset); return; }
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
        Canvas.SaveLayer(paint);
        painter(this, offset);
        Canvas.Restore();
    }

    /// <summary>Paints under <paramref name="transform"/> (which is applied about the origin of <paramref name="offset"/>).</summary>
    public void PushTransform(Offset offset, SKMatrix transform, Action<PaintingContext, Offset> painter)
    {
        Canvas.Save();
        Canvas.Translate(offset.Dx, offset.Dy);
        Canvas.Concat(in transform);
        painter(this, Offset.Zero);
        Canvas.Restore();
    }
}

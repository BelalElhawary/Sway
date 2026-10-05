using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Applies a blur and/or colour filter to everything painted by its child.</summary>
public sealed class RenderImageFilter(float blur, SKColorFilter? colorFilter) : RenderProxyBox
{
    float _blur = blur;
    SKColorFilter? _colorFilter = colorFilter;

    public void Update(float blur, SKColorFilter? colorFilter)
    {
        if (_blur == blur && ReferenceEquals(_colorFilter, colorFilter)) return;
        _blur = blur; _colorFilter = colorFilter;
        MarkNeedsPaint();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } c) return;
        if (_blur <= 0 && _colorFilter is null) { context.PaintChild(c, offset); return; }
        using var paint = new SKPaint { ColorFilter = _colorFilter };
        if (_blur > 0) paint.ImageFilter = SKImageFilter.CreateBlur(_blur, _blur);
        context.Canvas.SaveLayer(paint);
        context.PaintChild(c, offset);
        context.Canvas.Restore();
    }
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Filters whatever has already been painted behind it (within its own bounds), then paints its child on top.</summary>
public sealed class RenderBackdropFilter(float blur, SKColorFilter? colorFilter) : RenderProxyBox
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
        if (_blur <= 0 && _colorFilter is null)
        {
            if (Child is { } plain) context.PaintChild(plain, offset);
            return;
        }

        var canvas = context.Canvas;
        canvas.Save();
        canvas.ClipRect(Size.ToRect(offset).ToSk(), antialias: true);

        // Clamp keeps the blur from pulling in transparent black at the clip's edges.
        SKImageFilter? filter = _blur > 0 ? SKImageFilter.CreateBlur(_blur, _blur, SKShaderTileMode.Clamp) : null;
        if (_colorFilter is not null) filter = SKImageFilter.CreateColorFilter(_colorFilter, filter);

        canvas.SaveLayer(new SKCanvasSaveLayerRec { Backdrop = filter });
        if (Child is { } c) context.PaintChild(c, offset);
        canvas.Restore();
        canvas.Restore();
        filter?.Dispose();
    }
}

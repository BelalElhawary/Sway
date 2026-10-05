using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderCustomPaint(CustomPainter? painter, CustomPainter? foreground, Size preferred) : RenderProxyBox
{
    CustomPainter? _painter = painter, _foreground = foreground;
    Size _preferred = preferred;

    public void Update(CustomPainter? painter, CustomPainter? foreground, Size preferred)
    {
        bool repaint = (painter is null) != (_painter is null) || (painter is not null && _painter is not null && painter.ShouldRepaint(_painter))
            || (foreground is null) != (_foreground is null) || (foreground is not null && _foreground is not null && foreground.ShouldRepaint(_foreground));
        _painter = painter; _foreground = foreground;
        if (_preferred != preferred) { _preferred = preferred; MarkNeedsLayout(); }
        if (repaint) MarkNeedsPaint();
    }

    protected override void PerformLayout()
    {
        if (Child is { } c) { c.Layout(Constraints); Size = c.Size; }
        else Size = Constraints.Constrain(_preferred);
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        void Draw(CustomPainter p)
        {
            context.Canvas.Save();
            context.Canvas.Translate(offset.Dx, offset.Dy);
            p.Paint(context.Canvas, Size);
            context.Canvas.Restore();
        }
        if (_painter is not null) Draw(_painter);
        base.Paint(context, offset);
        if (_foreground is not null) Draw(_foreground);
    }

    protected override bool HitTestSelf(Offset position) => _painter?.HitTest(position) ?? false;
}

using SkiaSharp;

namespace Sway.Widgets;

sealed class RenderFractionalTranslation(Offset translation) : RenderProxyBox
{
    Offset _translation = translation;
    public Offset Translation { set { if (_translation == value) return; _translation = value; MarkNeedsPaint(); } }

    Offset Shift => new(_translation.Dx * Size.Width, _translation.Dy * Size.Height);

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + Shift);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithPaintOffset(Shift, position, c.HitTest);
}

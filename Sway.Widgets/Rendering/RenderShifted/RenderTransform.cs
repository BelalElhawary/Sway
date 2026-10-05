using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderTransform(SKMatrix transform, Alignment? origin) : RenderProxyBox
{
    SKMatrix _transform = transform;
    Alignment? _origin = origin;

    public void Update(SKMatrix transform, Alignment? origin)
    {
        if (_transform == transform && _origin == origin) return;
        _transform = transform; _origin = origin;
        MarkNeedsPaint();
    }

    SKMatrix Effective()
    {
        var o = (_origin ?? Alignment.Center).AlongSize(Size, Size.Zero);
        var toOrigin = SKMatrix.CreateTranslation(o.Dx, o.Dy);
        var fromOrigin = SKMatrix.CreateTranslation(-o.Dx, -o.Dy);
        // Apply: move origin to (0,0), transform, move back.
        return SKMatrix.Concat(toOrigin, SKMatrix.Concat(_transform, fromOrigin));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PushTransform(offset, Effective(), (ctx, o) => ctx.PaintChild(c, o));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithTransform(Effective(), position, c.HitTest);
}

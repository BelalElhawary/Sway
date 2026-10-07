using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderClip(BorderRadius? radius) : RenderProxyBox
{
    BorderRadius? _radius = radius;

    public BorderRadius? Radius
    {
        get => _radius;
        set { if (_radius == value) return; _radius = value; MarkNeedsPaint(); }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } c) return;
        var rect = Size.ToRect();
        if (_radius is { IsZero: false } r)
            context.PushClipRRect(offset, rect, r, (ctx, o) => ctx.PaintChild(c, o));
        else
            context.PushClipRect(offset, rect, (ctx, o) => ctx.PaintChild(c, o));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && c.HitTest(result, position);
}

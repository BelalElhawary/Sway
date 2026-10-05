using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderOpacity(float opacity) : RenderProxyBox
{
    float _opacity = opacity;

    public float Opacity
    {
        get => _opacity;
        set { if (_opacity == value) return; _opacity = value; MarkNeedsPaint(); }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PushOpacity(offset, _opacity, (ctx, o) => ctx.PaintChild(c, o));
    }
}

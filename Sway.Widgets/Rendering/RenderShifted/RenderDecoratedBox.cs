using SkiaSharp;

namespace Sway.Widgets;

public sealed class RenderDecoratedBox(BoxDecoration decoration, TextDirection direction, bool foreground = false) : RenderProxyBox
{
    BoxDecoration _decoration = decoration;
    TextDirection _direction = direction;

    public void Update(BoxDecoration decoration, TextDirection direction)
    {
        if (_decoration.Equals(decoration) && _direction == direction) return;
        _decoration = decoration;
        _direction = direction;
        MarkNeedsPaint();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (!foreground) _decoration.Paint(context.Canvas, Size.ToRect(offset), _direction);
        base.Paint(context, offset);
        if (foreground) _decoration.Paint(context.Canvas, Size.ToRect(offset), _direction);
    }

    protected override bool HitTestSelf(Offset position) => true;
}

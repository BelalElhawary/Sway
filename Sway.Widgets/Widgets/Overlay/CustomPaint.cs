using SkiaSharp;

namespace Sway.Widgets;

public sealed class CustomPaint(CustomPainter? painter = null, CustomPainter? foregroundPainter = null, Size? size = null,
    Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderCustomPaint(painter, foregroundPainter, size ?? Size.Zero);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderCustomPaint)ro).Update(painter, foregroundPainter, size ?? Size.Zero);
}

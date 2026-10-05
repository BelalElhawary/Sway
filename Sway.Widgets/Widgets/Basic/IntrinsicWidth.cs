using SkiaSharp;

namespace Sway.Widgets;

public sealed class IntrinsicWidth(Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderIntrinsic(Axis.Horizontal);
}

using SkiaSharp;

namespace Sway.Widgets;

public sealed class DecoratedBox(BoxDecoration decoration, Widget? child = null, bool foreground = false, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderDecoratedBox(decoration, Directionality.Of(context), foreground);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderDecoratedBox)ro).Update(decoration, Directionality.Of(context));
}

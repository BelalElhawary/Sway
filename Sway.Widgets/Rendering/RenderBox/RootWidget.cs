using SkiaSharp;

namespace Sway.Widgets;

public sealed class RootWidget(Widget? child) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderView();
}

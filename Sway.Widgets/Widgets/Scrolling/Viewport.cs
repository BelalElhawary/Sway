using SkiaSharp;

namespace Sway.Widgets;

/// <param name="shrinkWrap">Size the viewport to its child (up to the incoming maximum) instead of filling the maximum.</param>
public sealed class Viewport(Axis axis, ScrollPosition position, Widget? child, Key? key = null, bool shrinkWrap = false) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderViewport(axis, position, shrinkWrap);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderViewport)ro).Update(axis, position, shrinkWrap);
}

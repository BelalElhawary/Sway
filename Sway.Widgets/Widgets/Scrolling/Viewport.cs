using SkiaSharp;

namespace Sway.Widgets;

/// <param name="shrinkWrap">Size the viewport to its child (up to the incoming maximum) instead of filling the maximum.</param>
/// <param name="showScrollbar">Whether this viewport paints and handles its own scrollbar. Turn it off when another viewport sharing the position draws the one bar.</param>
public sealed class Viewport(Axis axis, ScrollPosition position, Widget? child, Key? key = null, bool shrinkWrap = false, bool showScrollbar = true) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderViewport(axis, position, shrinkWrap, showScrollbar);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderViewport)ro).Update(axis, position, shrinkWrap, showScrollbar);
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Makes its subtree invisible to hit testing.</summary>
public sealed class IgnorePointer(Widget? child = null, bool ignoring = true, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderIgnorePointer(ignoring);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderIgnorePointer)ro).Ignoring = ignoring;
}

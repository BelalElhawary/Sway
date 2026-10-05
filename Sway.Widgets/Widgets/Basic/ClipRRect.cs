using SkiaSharp;

namespace Sway.Widgets;

public sealed class ClipRRect(BorderRadius borderRadius, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderClip(borderRadius);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderClip)ro).Radius = borderRadius;
}

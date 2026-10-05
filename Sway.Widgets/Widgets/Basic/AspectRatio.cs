using SkiaSharp;

namespace Sway.Widgets;

public sealed class AspectRatio(float aspectRatio, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderAspectRatio(aspectRatio);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderAspectRatio)ro).Ratio = aspectRatio;
}

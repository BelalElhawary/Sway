using SkiaSharp;

namespace Sway.Widgets;

public sealed class Opacity(float opacity, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderOpacity(opacity);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderOpacity)ro).Opacity = opacity;
}

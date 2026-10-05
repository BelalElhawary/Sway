using SkiaSharp;

namespace Sway.Widgets;

public sealed class LimitedBox(float maxWidth = float.PositiveInfinity, float maxHeight = float.PositiveInfinity, Widget? child = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLimitedBox(maxWidth, maxHeight);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderLimitedBox)ro).Update(maxWidth, maxHeight);
}

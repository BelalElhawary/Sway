using SkiaSharp;

namespace Sway.Widgets;

public sealed class ConstrainedBox(BoxConstraints constraints, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderConstrainedBox(constraints);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderConstrainedBox)ro).AdditionalConstraints = constraints;
}

using SkiaSharp;

namespace Sway.Widgets;

public sealed class Align(IAlignment? alignment = null, Widget? child = null, float? widthFactor = null, float? heightFactor = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderPositionedBox(Resolve(context), widthFactor, heightFactor);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderPositionedBox)ro).Update(Resolve(context), widthFactor, heightFactor);
}

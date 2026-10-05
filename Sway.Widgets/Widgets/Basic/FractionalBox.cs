using SkiaSharp;

namespace Sway.Widgets;

sealed class FractionalBox(float? widthFactor, float? heightFactor, Widget? child) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFractionalBox(widthFactor, heightFactor);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFractionalBox)ro).Update(widthFactor, heightFactor);
}

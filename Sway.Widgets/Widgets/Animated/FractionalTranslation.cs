using SkiaSharp;

namespace Sway.Widgets;

public sealed class FractionalTranslation(Offset translation, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFractionalTranslation(translation);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFractionalTranslation)ro).Translation = translation;
}

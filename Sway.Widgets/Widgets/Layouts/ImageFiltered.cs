using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Blurs and/or recolours its child (the equivalent of CSS <c>filter</c>).</summary>
public sealed class ImageFiltered(Widget? child = null, float blur = 0, SKColorFilter? colorFilter = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderImageFilter(blur, colorFilter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderImageFilter)ro).Update(blur, colorFilter);
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Blurs and/or recolours what is behind it (the equivalent of CSS <c>backdrop-filter</c>), within its own bounds.</summary>
public sealed class BackdropFilter(Widget? child = null, float blur = 0, SKColorFilter? colorFilter = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderBackdropFilter(blur, colorFilter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderBackdropFilter)ro).Update(blur, colorFilter);
}

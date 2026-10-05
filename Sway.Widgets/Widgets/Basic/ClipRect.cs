using SkiaSharp;

namespace Sway.Widgets;

public sealed class ClipRect(Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderClip(null);
}

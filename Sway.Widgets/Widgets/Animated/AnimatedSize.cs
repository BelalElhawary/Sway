using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedSize(TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderAnimatedSize(duration, curve ?? Curves.Linear, alignment ?? Alignment.TopCenter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderAnimatedSize)ro).Update(duration, curve ?? Curves.Linear, alignment ?? Alignment.TopCenter);
}

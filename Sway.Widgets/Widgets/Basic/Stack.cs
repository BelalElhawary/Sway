using SkiaSharp;

namespace Sway.Widgets;

public sealed class Stack(IReadOnlyList<Widget> children, IAlignment? alignment = null, StackFit fit = StackFit.Loose,
    bool clip = true, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? AlignmentDirectional.TopStart).Resolve(Directionality.Of(c));
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderStack(Resolve(context), fit, clip);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderStack)ro).Update(Resolve(context), fit, clip);
}

using SkiaSharp;

namespace Sway.Widgets;

public sealed class Padding(IEdgeInsetsLike padding, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public Padding(EdgeInsets padding, Widget? child = null, Key? key = null) : this(new PhysicalInsets(padding), child, key) { }
    public Padding(EdgeInsetsDirectional padding, Widget? child = null, Key? key = null) : this(new DirectionalInsets(padding), child, key) { }

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderPadding(padding.Resolve(Directionality.Of(context)));

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderPadding)ro).Padding = padding.Resolve(Directionality.Of(context));
}

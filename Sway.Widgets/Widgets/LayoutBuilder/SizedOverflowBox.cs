namespace Sway.Widgets;

/// <summary>A box of a fixed size whose child is laid out with the parent's constraints and may overflow it.</summary>
public sealed class SizedOverflowBox(Size size, Widget? child = null, IAlignment? alignment = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) => new RenderSizedOverflowBox(Resolve(context), size);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderSizedOverflowBox)ro).Update(Resolve(context), size);
}

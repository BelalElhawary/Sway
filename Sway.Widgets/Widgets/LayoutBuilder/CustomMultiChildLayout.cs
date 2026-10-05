namespace Sway.Widgets;

/// <summary>Lays out children with a <see cref="MultiChildLayoutDelegate"/>; each child must be a <see cref="LayoutId"/>.</summary>
public sealed class CustomMultiChildLayout(MultiChildLayoutDelegate layoutDelegate, IReadOnlyList<Widget> children, Key? key = null)
    : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderCustomMultiChildLayout(layoutDelegate);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderCustomMultiChildLayout)ro).Delegate = layoutDelegate;
}

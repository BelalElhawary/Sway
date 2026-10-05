namespace Sway.Widgets;

/// <summary>Tags a child of <see cref="CustomMultiChildLayout"/> with the id its delegate lays it out by.</summary>
public sealed class LayoutId(object id, Widget child, Key? key = null) : ParentDataWidget(child, key)
{
    public object Id => id;

    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderCustomMultiChildLayout;

    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not MultiChildLayoutParentData pd) ro.ParentData = pd = new MultiChildLayoutParentData();
        if (Equals(pd.Id, id)) return;
        pd.Id = id;
        ro.Parent?.MarkNeedsLayout();
    }
}

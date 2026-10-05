namespace Sway.Widgets;

public sealed class RenderCustomMultiChildLayout(MultiChildLayoutDelegate layoutDelegate) : RenderBoxContainer
{
    MultiChildLayoutDelegate _delegate = layoutDelegate;

    public MultiChildLayoutDelegate Delegate
    {
        get => _delegate;
        set
        {
            if (ReferenceEquals(_delegate, value)) return;
            bool relayout = value.ShouldRelayout(_delegate);
            _delegate = value;
            if (relayout) MarkNeedsLayout();
        }
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not MultiChildLayoutParentData) child.ParentData = new MultiChildLayoutParentData();
    }

    protected override void PerformLayout()
    {
        Size = Constraints.Constrain(_delegate.GetSize(Constraints));
        _delegate.Run(this, Size);
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }
}

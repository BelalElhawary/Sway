namespace Sway.Widgets;

public sealed class ParentDataElement : ProxyElement
{
    public ParentDataElement(ParentDataWidget widget) : base(widget) { }

    protected override Widget Child => ((ParentDataWidget)Widget).Child;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        Apply();
    }

    protected override void Updated(Widget oldWidget) { }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        Apply();
        // Parent data changes affect the parent's layout.
        FindRenderObject()?.Parent?.MarkNeedsLayout();
    }

    internal void Apply()
    {
        if (FindRenderObject() is { } ro) ((ParentDataWidget)Widget).ApplyParentData(ro);
    }
}

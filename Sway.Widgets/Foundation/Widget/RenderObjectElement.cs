namespace Sway.Widgets;

public abstract class RenderObjectElement : Element
{
    protected RenderObjectElement(RenderObjectWidget widget) : base(widget) { }

    public RenderObject RenderObject { get; private set; } = null!;
    new RenderObjectWidget Widget => (RenderObjectWidget)base.Widget;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        RenderObject = Widget.CreateRenderObject(this);
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        Widget.UpdateRenderObject(this, RenderObject);
    }

    public override void Unmount()
    {
        base.Unmount();
        Widget.DidUnmountRenderObject(RenderObject);
        RenderObject.Dispose();
    }

    protected override void PerformRebuild() { }

    internal abstract void ResyncChildren();
}

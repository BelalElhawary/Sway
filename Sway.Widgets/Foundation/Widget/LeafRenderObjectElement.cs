namespace Sway.Widgets;

public sealed class LeafRenderObjectElement(LeafRenderObjectWidget widget) : RenderObjectElement(widget)
{
    public override void VisitChildren(Action<Element> visitor) { }
    internal override void ResyncChildren() { }
}

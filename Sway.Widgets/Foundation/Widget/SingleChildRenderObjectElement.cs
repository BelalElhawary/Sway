namespace Sway.Widgets;

public sealed class SingleChildRenderObjectElement : RenderObjectElement
{
    Element? _child;

    public SingleChildRenderObjectElement(SingleChildRenderObjectWidget widget) : base(widget) { }

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        _child = UpdateChild(null, ((SingleChildRenderObjectWidget)Widget).Child);
        ResyncChildren();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _child = UpdateChild(_child, ((SingleChildRenderObjectWidget)Widget).Child);
        ResyncChildren();
    }

    internal override void ResyncChildren() =>
        ((IRenderChildHolder)RenderObject).Child = _child?.FindRenderObject() as RenderBox;

    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}

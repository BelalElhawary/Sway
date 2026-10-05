namespace Sway.Widgets;

public sealed class MultiChildRenderObjectElement : RenderObjectElement
{
    List<Element> _children = new();

    public MultiChildRenderObjectElement(MultiChildRenderObjectWidget widget) : base(widget) { }

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        var widgets = ((MultiChildRenderObjectWidget)Widget).Children;
        _children = UpdateChildren(Array.Empty<Element>(), widgets);
        ResyncChildren();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _children = UpdateChildren(_children, ((MultiChildRenderObjectWidget)Widget).Children);
        ResyncChildren();
    }

    internal override void ResyncChildren()
    {
        var boxes = new List<RenderBox>(_children.Count);
        foreach (var c in _children)
            if (c.FindRenderObject() is RenderBox b) boxes.Add(b);
        ((RenderBoxContainer)RenderObject).SetChildren(boxes);
    }

    public override void VisitChildren(Action<Element> visitor)
    {
        foreach (var c in _children) visitor(c);
    }
}

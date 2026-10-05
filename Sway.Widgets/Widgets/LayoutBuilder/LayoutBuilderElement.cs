namespace Sway.Widgets;

sealed class LayoutBuilderElement : RenderObjectElement
{
    Element? _child;
    BoxConstraints? _builtFor;
    bool _needsBuild = true;

    public LayoutBuilderElement(LayoutBuilder widget) : base(widget) { }

    RenderLayoutBuilder RO => (RenderLayoutBuilder)RenderObject;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        RO.BuildChild = BuildChild;
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _needsBuild = true;
        RO.MarkNeedsLayout();
    }

    internal override void DidChangeDependencies()
    {
        // The builder may read inherited widgets (themes, media queries); rebuild it on the next layout.
        _needsBuild = true;
        RO.MarkNeedsLayout();
    }

    void BuildChild(BoxConstraints constraints)
    {
        if (!_needsBuild && _builtFor == constraints) return;
        _needsBuild = false;
        _builtFor = constraints;
        _child = UpdateChild(_child, ((LayoutBuilder)Widget).Builder(this, constraints));
        ResyncChildren();
    }

    internal override void ResyncChildren() => RO.Child = _child?.FindRenderObject() as RenderBox;

    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}

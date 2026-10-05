namespace Sway.Widgets;

public abstract class ComponentElement : Element
{
    Element? _child;

    protected ComponentElement(Widget widget) : base(widget) { }

    protected abstract Widget Build();

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        FirstBuild();
    }

    protected virtual void FirstBuild() => _child = UpdateChild(null, BuildSafe());

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        ClearDirty();
        Rebuild();
    }

    protected override void PerformRebuild() => Rebuild();

    protected void Rebuild()
    {
        var before = _child?.FindRenderObject();
        _child = UpdateChild(_child, BuildSafe());
        var after = _child?.FindRenderObject();
        if (!ReferenceEquals(before, after)) NotifyRenderObjectChanged();
    }

    Widget BuildSafe()
    {
        try { return Build(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Build error in {Widget.GetType().Name}: {ex}");
            return new ErrorWidget(ex.Message);
        }
    }

    /// <summary>The render object below this element was replaced; the nearest render parent must re-adopt it.</summary>
    internal void NotifyRenderObjectChanged()
    {
        for (var e = Parent; e is not null; e = e.Parent)
        {
            if (e is RenderObjectElement r) { r.ResyncChildren(); return; }
            if (e is ParentDataElement p) p.Apply();
        }
    }

    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }
}

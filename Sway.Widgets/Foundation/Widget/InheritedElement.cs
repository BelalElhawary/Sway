namespace Sway.Widgets;

public sealed class InheritedElement : ProxyElement
{
    readonly HashSet<Element> _dependents = new();

    public InheritedElement(InheritedWidget widget) : base(widget) { }

    protected override Widget Child => ((InheritedWidget)Widget).Child;

    protected override Dictionary<Type, InheritedElement>? InheritedTableFor(Element? parent)
    {
        var table = parent?.InheritedTable is { } p ? new Dictionary<Type, InheritedElement>(p) : new();
        table[Widget.GetType()] = this;
        return table;
    }

    internal void AddDependent(Element e) => _dependents.Add(e);
    internal void RemoveDependent(Element e) => _dependents.Remove(e);

    protected override void Updated(Widget oldWidget)
    {
        if (((InheritedWidget)Widget).UpdateShouldNotify((InheritedWidget)oldWidget))
            foreach (var d in _dependents.ToArray()) d.DidChangeDependencies();
    }
}

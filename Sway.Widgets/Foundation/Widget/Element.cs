namespace Sway.Widgets;

public abstract class Element : BuildContext
{
    Dictionary<Type, InheritedElement>? _inherited;
    bool _dirty;

    protected Element(Widget widget) { Widget = widget; }

    public Widget Widget { get; internal set; }
    public Element? Parent { get; private set; }
    public int Depth { get; private set; }
    public bool Mounted { get; private set; }
    public BuildOwner? Owner { get; internal set; }

    Size? BuildContext.Size => (FindRenderObject() as RenderBox)?.SizeOrNull;

    public virtual void Mount(Element? parent)
    {
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Owner ??= parent?.Owner;
        _inherited = InheritedTableFor(parent);
        Mounted = true;
    }

    /// <summary>The inherited-widget table descendants see; <see cref="InheritedElement"/> adds itself.</summary>
    protected virtual Dictionary<Type, InheritedElement>? InheritedTableFor(Element? parent) => parent?._inherited;

    public virtual void Update(Widget newWidget) => Widget = newWidget;

    public virtual void Unmount()
    {
        VisitChildren(c => c.Unmount());
        if (_inherited is not null)
            foreach (var ie in _inherited.Values) ie.RemoveDependent(this);
        Mounted = false;
    }

    public abstract void VisitChildren(Action<Element> visitor);

    public void MarkNeedsBuild()
    {
        if (!Mounted || _dirty) return;
        _dirty = true;
        Owner!.ScheduleBuildFor(this);
    }

    internal void RebuildIfNeeded()
    {
        if (!_dirty) return;
        _dirty = false;
        PerformRebuild();
    }

    protected void ClearDirty() => _dirty = false;

    protected abstract void PerformRebuild();

    public T? DependOn<T>() where T : InheritedWidget
    {
        if (_inherited is not null && _inherited.TryGetValue(typeof(T), out var ie))
        {
            ie.AddDependent(this);
            return (T)ie.Widget;
        }
        return null;
    }

    public T? Get<T>() where T : InheritedWidget =>
        _inherited is not null && _inherited.TryGetValue(typeof(T), out var ie) ? (T)ie.Widget : null;

    internal Dictionary<Type, InheritedElement>? InheritedTable => _inherited;

    internal virtual void DidChangeDependencies() => MarkNeedsBuild();

    public RenderObject? FindRenderObject()
    {
        if (this is RenderObjectElement r) return r.RenderObject;
        RenderObject? found = null;
        VisitChildren(c => found ??= c.FindRenderObject());
        return found;
    }

    /// <summary>Adds, updates or removes a single child so it matches <paramref name="newWidget"/>.</summary>
    protected Element? UpdateChild(Element? child, Widget? newWidget)
    {
        if (newWidget is null)
        {
            child?.Unmount();
            return null;
        }
        if (child is not null)
        {
            if (ReferenceEquals(child.Widget, newWidget)) return child;
            if (Widget.CanUpdate(child.Widget, newWidget))
            {
                child.Update(newWidget);
                return child;
            }
            child.Unmount();
        }
        var element = newWidget.CreateElement();
        element.Mount(this);
        return element;
    }

    /// <summary>Reconciles a child list: matches from both ends, then by key in the middle.</summary>
    protected List<Element> UpdateChildren(IReadOnlyList<Element> oldChildren, IReadOnlyList<Widget> newWidgets)
    {
        var result = new Element?[newWidgets.Count];
        int newTop = 0, oldTop = 0;
        int newBottom = newWidgets.Count - 1, oldBottom = oldChildren.Count - 1;

        while (oldTop <= oldBottom && newTop <= newBottom && Widget.CanUpdate(oldChildren[oldTop].Widget, newWidgets[newTop]))
        {
            result[newTop] = UpdateChild(oldChildren[oldTop], newWidgets[newTop]);
            newTop++; oldTop++;
        }

        int tailOld = oldBottom, tailNew = newBottom;
        while (oldTop <= oldBottom && newTop <= newBottom && Widget.CanUpdate(oldChildren[oldBottom].Widget, newWidgets[newBottom]))
        {
            oldBottom--; newBottom--;
        }

        Dictionary<Key, Element>? keyed = null;
        if (oldTop <= oldBottom)
        {
            keyed = new();
            for (int i = oldTop; i <= oldBottom; i++)
            {
                var old = oldChildren[i];
                if (old.Widget.Key is { } k) keyed[k] = old;
                else old.Unmount();
            }
        }

        for (; newTop <= newBottom; newTop++)
        {
            var w = newWidgets[newTop];
            Element? old = null;
            if (w.Key is { } k && keyed is not null && keyed.Remove(k, out old) && !Widget.CanUpdate(old.Widget, w))
            {
                old.Unmount();
                old = null;
            }
            result[newTop] = UpdateChild(old, w);
        }

        if (keyed is not null)
            foreach (var leftover in keyed.Values) leftover.Unmount();

        for (int o = oldBottom + 1, n = newBottom + 1; o <= tailOld; o++, n++)
            result[n] = UpdateChild(oldChildren[o], newWidgets[n]);

        return result.Select(e => e!).ToList();
    }
}

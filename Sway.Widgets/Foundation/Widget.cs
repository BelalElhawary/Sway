namespace Sway.Widgets;

/// <summary>Identifies a widget across rebuilds so its <see cref="State"/> follows it when siblings reorder.</summary>
public abstract class Key { }

public sealed class ValueKey<T>(T value) : Key where T : notnull
{
    public T Value { get; } = value;
    public override bool Equals(object? obj) => obj is ValueKey<T> k && EqualityComparer<T>.Default.Equals(Value, k.Value);
    public override int GetHashCode() => HashCode.Combine(typeof(T), Value);
}

/// <summary>Key by reference identity of an object.</summary>
public sealed class ObjectKey(object value) : Key
{
    public object Value { get; } = value;
    public override bool Equals(object? obj) => obj is ObjectKey k && ReferenceEquals(Value, k.Value);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value);
}

/// <summary>Unique key. Two <see cref="UniqueKey"/>s are never equal.</summary>
public sealed class UniqueKey : Key { }

/// <summary>An immutable description of part of the UI. Widgets are cheap; elements and render objects are not.</summary>
public abstract class Widget(Key? key = null)
{
    public Key? Key { get; } = key;

    public abstract Element CreateElement();

    public static bool CanUpdate(Widget oldWidget, Widget newWidget) =>
        oldWidget.GetType() == newWidget.GetType() && Equals(oldWidget.Key, newWidget.Key);
}

public abstract class StatelessWidget(Key? key = null) : Widget(key)
{
    public abstract Widget Build(BuildContext context);
    public override Element CreateElement() => new StatelessElement(this);
}

public abstract class StatefulWidget(Key? key = null) : Widget(key)
{
    public abstract State CreateState();
    public override Element CreateElement() => new StatefulElement(this);
}

/// <summary>Widget whose descendants can read it with <c>context.DependOn&lt;T&gt;()</c> and rebuild when it changes.</summary>
public abstract class InheritedWidget(Widget child, Key? key = null) : Widget(key)
{
    public Widget Child { get; } = child;
    public abstract bool UpdateShouldNotify(InheritedWidget oldWidget);
    public override Element CreateElement() => new InheritedElement(this);
}

/// <summary>Widget that configures the parent data of the nearest descendant render object (Expanded, Positioned...).</summary>
public abstract class ParentDataWidget(Widget child, Key? key = null) : Widget(key)
{
    public Widget Child { get; } = child;
    public abstract bool DebugIsValidParent(RenderObject parent);
    public abstract void ApplyParentData(RenderObject renderObject);
    public override Element CreateElement() => new ParentDataElement(this);
}

public abstract class RenderObjectWidget(Key? key = null) : Widget(key)
{
    public abstract RenderObject CreateRenderObject(BuildContext context);
    public virtual void UpdateRenderObject(BuildContext context, RenderObject renderObject) { }
    public virtual void DidUnmountRenderObject(RenderObject renderObject) { }
}

public abstract class LeafRenderObjectWidget(Key? key = null) : RenderObjectWidget(key)
{
    public override Element CreateElement() => new LeafRenderObjectElement(this);
}

public abstract class SingleChildRenderObjectWidget(Widget? child, Key? key = null) : RenderObjectWidget(key)
{
    public Widget? Child { get; } = child;
    public override Element CreateElement() => new SingleChildRenderObjectElement(this);
}

public abstract class MultiChildRenderObjectWidget(IReadOnlyList<Widget> children, Key? key = null) : RenderObjectWidget(key)
{
    public IReadOnlyList<Widget> Children { get; } = children;
    public override Element CreateElement() => new MultiChildRenderObjectElement(this);
}

/// <summary>The persistent mutable half of a <see cref="StatefulWidget"/>.</summary>
public abstract class State
{
    internal StatefulElement? Element;
    internal StatefulWidget? WidgetInternal;

    public BuildContext Context => Element ?? throw new InvalidOperationException("State is not mounted.");
    public bool Mounted => Element is { Mounted: true };

    public virtual void InitState() { }
    public virtual void DidUpdateWidget(StatefulWidget oldWidget) { }
    public virtual void DidChangeDependencies() { }
    public virtual void Dispose() { }
    public abstract Widget Build(BuildContext context);

    /// <summary>Runs <paramref name="fn"/> then schedules a rebuild. Safe to call from any thread that owns the UI.</summary>
    public void SetState(Action? fn = null)
    {
        fn?.Invoke();
        if (Element is { Mounted: true } e) e.MarkNeedsBuild();
    }
}

public abstract class State<T> : State where T : StatefulWidget
{
    public T Widget => (T)WidgetInternal!;
    public sealed override void DidUpdateWidget(StatefulWidget oldWidget) => DidUpdateWidget((T)oldWidget);
    public virtual void DidUpdateWidget(T oldWidget) { }
}

/// <summary>A handle to a widget's location in the element tree.</summary>
public interface BuildContext
{
    Widget Widget { get; }
    bool Mounted { get; }
    /// <summary>The laid-out size of the nearest render box at or below this element.</summary>
    Size? Size { get; }
    /// <summary>Finds the nearest ancestor inherited widget of type T and registers for rebuilds when it changes.</summary>
    T? DependOn<T>() where T : InheritedWidget;
    /// <summary>Like <see cref="DependOn{T}"/> but does not rebuild when the widget changes.</summary>
    T? Get<T>() where T : InheritedWidget;
    RenderObject? FindRenderObject();
}

/// <summary>Anything that can wake the host and ask for a frame.</summary>
public sealed class BuildOwner
{
    readonly List<Element> _dirty = new();
    bool _scheduled;

    public Action? OnBuildScheduled { get; set; }
    public bool HasDirty => _dirty.Count > 0;

    internal void ScheduleBuildFor(Element element)
    {
        _dirty.Add(element);
        if (!_scheduled) { _scheduled = true; OnBuildScheduled?.Invoke(); }
    }

    /// <summary>Rebuilds dirty elements shallowest first; rebuilds that dirty more elements are folded into the same pass.</summary>
    public void BuildScope()
    {
        while (_dirty.Count > 0)
        {
            _dirty.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            var batch = _dirty.ToArray();
            _dirty.Clear();
            foreach (var e in batch)
                if (e.Mounted) e.RebuildIfNeeded();
        }
        _scheduled = false;
    }
}

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

public sealed class StatelessElement(StatelessWidget widget) : ComponentElement(widget)
{
    protected override Widget Build() => ((StatelessWidget)Widget).Build(this);
}

public sealed class StatefulElement : ComponentElement
{
    readonly State _state;

    public StatefulElement(StatefulWidget widget) : base(widget)
    {
        _state = widget.CreateState();
        _state.Element = this;
        _state.WidgetInternal = widget;
    }

    public State State => _state;

    protected override Widget Build() => _state.Build(this);

    protected override void FirstBuild()
    {
        // InitState runs after the element is linked into the tree, so it may read inherited widgets.
        _state.InitState();
        _state.DidChangeDependencies();
        base.FirstBuild();
    }

    public override void Update(Widget newWidget)
    {
        var old = (StatefulWidget)Widget;
        _state.WidgetInternal = (StatefulWidget)newWidget;
        base.Widget = newWidget;
        _state.DidUpdateWidget(old);
        ClearDirty();
        Rebuild();
    }

    internal override void DidChangeDependencies()
    {
        _state.DidChangeDependencies();
        base.DidChangeDependencies();
    }

    public override void Unmount()
    {
        _state.Dispose();
        base.Unmount();
    }
}

/// <summary>An element with exactly one widget child (Inherited, ParentData).</summary>
public abstract class ProxyElement : ComponentElement
{
    protected ProxyElement(Widget widget) : base(widget) { }
    protected abstract Widget Child { get; }
    protected override Widget Build() => Child;

    public override void Update(Widget newWidget)
    {
        var old = Widget;
        base.Widget = newWidget;
        Updated(old);
        ClearDirty();
        Rebuild();
    }

    protected virtual void Updated(Widget oldWidget) { }
}

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

public sealed class LeafRenderObjectElement(LeafRenderObjectWidget widget) : RenderObjectElement(widget)
{
    public override void VisitChildren(Action<Element> visitor) { }
    internal override void ResyncChildren() { }
}

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

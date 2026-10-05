namespace Sway.Widgets;

/// <summary>A keyboard event with DOM-style key names ("Enter", "ArrowLeft", "a").</summary>
public sealed record KeyEvent(string Key, string Code, bool IsDown, bool Ctrl, bool Shift, bool Alt, bool Meta, bool Repeat = false)
{
    /// <summary>Ctrl on Windows/Linux (Cmd is not mapped separately).</summary>
    public bool Command => Ctrl || Meta;
}

public sealed class FocusNode
{
    internal Element? Element;

    public FocusNode? Parent { get; internal set; }
    public bool CanRequestFocus { get; set; } = true;
    public bool SkipTraversal { get; set; }
    public string? DebugLabel { get; set; }

    /// <summary>While focus is inside this node, Tab and Shift+Tab cycle only through its descendants (modal dialogs, menus).</summary>
    public bool TrapsFocus { get; set; }

    /// <summary>Explicit Tab position: lower comes first, ties and unset nodes (0) fall back to tree order. See <see cref="FocusTraversalOrder"/>.</summary>
    public float Order { get; set; }

    /// <summary>Called for key events while this node or a descendant has focus; return true to consume.</summary>
    public Func<KeyEvent, bool>? OnKey { get; set; }

    /// <summary>Receives typed characters when this node holds the primary focus.</summary>
    public Action<string>? OnTextInput { get; set; }

    public event Action? Changed;

    public bool HasPrimaryFocus => WidgetsBinding.Instance.Focus.Primary == this;

    /// <summary>True if this node or any descendant has primary focus.</summary>
    public bool HasFocus
    {
        get
        {
            for (var n = WidgetsBinding.Instance.Focus.Primary; n is not null; n = n.Parent)
                if (n == this) return true;
            return false;
        }
    }

    public void RequestFocus() => WidgetsBinding.Instance.Focus.Focus(this);
    public void Unfocus() { if (HasFocus) WidgetsBinding.Instance.Focus.Focus(null); }

    internal void NotifyChanged() => Changed?.Invoke();
}

/// <summary>Tracks the primary focus, routes keyboard input to it and implements Tab traversal.</summary>
public sealed class FocusManager
{
    readonly List<FocusNode> _nodes = new();
    FocusNode? _primary;
    bool _focusVisible;

    public FocusNode? Primary => _primary;

    /// <summary>True when focus arrived via keyboard, so widgets can show a focus ring only then.</summary>
    public bool FocusVisible => _focusVisible;

    internal void Register(FocusNode node) { if (!_nodes.Contains(node)) _nodes.Add(node); }

    internal void Unregister(FocusNode node)
    {
        _nodes.Remove(node);
        if (_primary == node || IsAncestor(node, _primary)) Focus(null);
    }

    static bool IsAncestor(FocusNode ancestor, FocusNode? n)
    {
        for (; n is not null; n = n.Parent) if (n == ancestor) return true;
        return false;
    }

    public void Focus(FocusNode? node, bool viaKeyboard = false)
    {
        if (node is { CanRequestFocus: false }) return;
        _focusVisible = viaKeyboard && node is not null;
        if (_primary == node) return;

        var before = Chain(_primary);
        _primary = node;
        var after = Chain(node);
        foreach (var n in before.Except(after)) n.NotifyChanged();
        foreach (var n in after.Except(before)) n.NotifyChanged();
        if (viaKeyboard && node is not null) ScrollIntoView(node);
        WidgetsBinding.Instance.RequestFrame();
    }

    /// <summary>Scrolls every enclosing scrollable just far enough that the focused widget is visible.</summary>
    static void ScrollIntoView(FocusNode node)
    {
        if (node.Element?.FindRenderObject() is not RenderBox box || box.SizeOrNull is not { } size) return;
        for (RenderObject? o = box.Parent; o is not null; o = o.Parent)
        {
            if (o is not IScrollViewport viewport || o is not RenderBox view || view.SizeOrNull is not { } viewSize) continue;
            var local = box.LocalToGlobal(Offset.Zero) - view.LocalToGlobal(Offset.Zero) + viewport.PaintShift;
            bool vertical = viewport.ScrollAxis == Axis.Vertical;
            float start = vertical ? local.Dy : local.Dx;
            float extent = vertical ? size.Height : size.Width;
            float visible = vertical ? viewSize.Height : viewSize.Width;
            if (start < 0) viewport.Position.JumpTo(viewport.Position.Pixels + start);
            else if (start + extent > visible) viewport.Position.JumpTo(viewport.Position.Pixels + start + extent - visible);
            // The next layout moves the child; later ancestors need its new place, so stop after the nearest scrollable.
            return;
        }
    }

    static List<FocusNode> Chain(FocusNode? n)
    {
        var list = new List<FocusNode>();
        for (; n is not null; n = n.Parent) list.Add(n);
        return list;
    }

    /// <summary>Dispatches from the primary focus up through its ancestors; unhandled Tab moves focus.</summary>
    public bool HandleKey(KeyEvent e)
    {
        for (var n = _primary; n is not null; n = n.Parent)
            if (n.OnKey?.Invoke(e) == true) return true;

        if (e.IsDown && e.Key == "Tab" && !e.Ctrl && !e.Alt)
        {
            Traverse(e.Shift ? -1 : 1);
            return true;
        }
        return false;
    }

    public void HandleText(string text)
    {
        var n = _primary;
        if (n is { OnTextInput: not null }) n.OnTextInput(text);
    }

    public void Traverse(int direction)
    {
        var trap = ActiveTrap();
        var order = _nodes.Where(n => n.CanRequestFocus && !n.SkipTraversal && n.Element is { Mounted: true } && (trap is null || IsAncestor(trap, n)))
            .OrderBy(n => n.Order).ThenBy(n => TreeOrder(n.Element!), Comparer<long[]>.Create(CompareKeys)).ToList();
        if (order.Count == 0) return;
        int i = _primary is null ? -1 : order.IndexOf(_primary);
        int next = i < 0 ? (direction > 0 ? 0 : order.Count - 1) : (i + direction + order.Count) % order.Count;
        Focus(order[next], viaKeyboard: true);
    }

    // The innermost focus-trapping ancestor of the primary focus, or (with nothing focused) the most recently opened trap.
    FocusNode? ActiveTrap()
    {
        for (var n = _primary; n is not null; n = n.Parent)
            if (n.TrapsFocus) return n;
        return _primary is null ? _nodes.LastOrDefault(n => n.TrapsFocus && n.Element is { Mounted: true }) : null;
    }

    // Depth-first position of an element: the child index at each level from the root.
    static long[] TreeOrder(Element e)
    {
        var path = new List<long>();
        for (var cur = e; cur.Parent is { } p; cur = p)
        {
            int idx = 0, found = 0;
            p.VisitChildren(c => { if (ReferenceEquals(c, cur)) found = idx; idx++; });
            path.Add(found);
        }
        path.Reverse();
        return path.ToArray();
    }

    static int CompareKeys(long[]? a, long[]? b)
    {
        for (int i = 0; i < Math.Min(a!.Length, b!.Length); i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return a.Length.CompareTo(b.Length);
    }
}

/// <summary>Makes a subtree focusable and exposes its <see cref="FocusNode"/> to descendants.</summary>
public sealed class Focus(Widget child, FocusNode? focusNode = null, bool autofocus = false, Func<KeyEvent, bool>? onKey = null,
    Action<bool>? onFocusChange = null, bool canRequestFocus = true, bool skipTraversal = false, bool trapFocus = false, Key? key = null) : StatefulWidget(key)
{
    internal Widget Child => child;
    internal FocusNode? Node => focusNode;
    internal bool Autofocus => autofocus;
    internal Func<KeyEvent, bool>? OnKey => onKey;
    internal Action<bool>? OnFocusChange => onFocusChange;
    internal bool CanRequestFocus => canRequestFocus;
    internal bool SkipTraversal => skipTraversal;
    internal bool TrapFocus => trapFocus;

    public override State CreateState() => new FocusState();

    /// <summary>The nearest enclosing focus node (does not rebuild on focus changes).</summary>
    public static FocusNode? MaybeOf(BuildContext context) => context.Get<FocusScopeMarker>()?.Node;

    /// <summary>True if the nearest enclosing Focus (or a descendant of it) has focus; rebuilds when that changes.</summary>
    public static bool IsFocused(BuildContext context) => context.DependOn<FocusScopeMarker>()?.HasFocus ?? false;
}

sealed class FocusScopeMarker(FocusNode node, bool hasFocus, Widget child) : InheritedWidget(child)
{
    public FocusNode Node => node;
    public bool HasFocus => hasFocus;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((FocusScopeMarker)old).HasFocus != HasFocus || ((FocusScopeMarker)old).Node != Node;
}

sealed class FocusState : State<Focus>
{
    FocusNode? _owned;
    FocusNode _node = null!;
    bool _hadFocus;

    public override void InitState()
    {
        Attach();
        if (Widget.Autofocus)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted) _node.RequestFocus(); });
    }

    void Attach()
    {
        _node = Widget.Node ?? (_owned ??= new FocusNode());
        _node.Element = (Element?)Context;
        _node.CanRequestFocus = Widget.CanRequestFocus;
        _node.SkipTraversal = Widget.SkipTraversal;
        _node.TrapsFocus = Widget.TrapFocus;
        _node.Order = FocusTraversalOrder.Of(Context);
        _node.Parent = Focus.MaybeOf(Context);
        _node.Changed += OnChanged;
        WidgetsBinding.Instance.Focus.Register(_node);
        if (Widget.OnKey is not null) _node.OnKey = Widget.OnKey;
        _hadFocus = _node.HasFocus;
    }

    void Detach()
    {
        _node.Changed -= OnChanged;
        WidgetsBinding.Instance.Focus.Unregister(_node);
        _node.Element = null;
    }

    void OnChanged()
    {
        bool has = _node.HasFocus;
        if (has == _hadFocus) return;
        _hadFocus = has;
        Widget.OnFocusChange?.Invoke(has);
        if (Mounted) SetState();
    }

    public override void DidUpdateWidget(Focus old)
    {
        if (!ReferenceEquals(old.Node, Widget.Node) && !(old.Node is null && Widget.Node is null))
        {
            Detach();
            Attach();
        }
        else
        {
            _node.CanRequestFocus = Widget.CanRequestFocus;
            _node.SkipTraversal = Widget.SkipTraversal;
            _node.TrapsFocus = Widget.TrapFocus;
            _node.Order = FocusTraversalOrder.Of(Context);
            if (Widget.OnKey is not null) _node.OnKey = Widget.OnKey;
        }
    }

    public override void Dispose() => Detach();

    public override Widget Build(BuildContext context) =>
        new FocusScopeMarker(_node, _node.HasFocus, Widget.Child);
}

/// <summary>Sets the Tab position of the focusable widgets below it: lower numbers are visited first, others (0) follow tree order.</summary>
public sealed class FocusTraversalOrder(float order, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public float Order => order;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((FocusTraversalOrder)old).Order != Order;
    internal static float Of(BuildContext context) => context.Get<FocusTraversalOrder>()?.Order ?? 0;
}

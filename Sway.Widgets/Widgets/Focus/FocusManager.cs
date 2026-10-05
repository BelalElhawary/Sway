namespace Sway.Widgets;

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
            var local = box.LocalToGlobal(Offset.Zero) - view.LocalToGlobal(Offset.Zero);
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

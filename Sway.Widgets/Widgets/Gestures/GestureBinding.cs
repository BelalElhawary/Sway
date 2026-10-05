namespace Sway.Widgets;

/// <summary>Hit tests pointer input, dispatches it to render objects, tracks hover state and the cursor.</summary>
public sealed class GestureBinding(WidgetsBinding binding)
{
    const int MousePointer = 0;

    readonly Dictionary<int, IReadOnlyList<HitTestEntry>> _paths = new();
    List<RenderMouseRegion> _hovered = new();
    Offset _lastPosition;

    public GestureArena Arena { get; } = new();
    public PointerRouter Router { get; } = new();
    public MouseCursor Cursor { get; private set; } = MouseCursor.Default;
    public Offset PointerPosition => _lastPosition;

    HitTestResult HitTest(Offset position) => binding.RenderView.HitTestAt(position);

    void Dispatch(IReadOnlyList<HitTestEntry> path, PointerEvent e)
    {
        foreach (var entry in path.ToArray())
            entry.Target.HandlePointerEvent(e with { LocalPosition = entry.ToLocal(e.Position) }, entry);
    }

    public void PointerDown(float x, float y)
    {
        var p = new Offset(x, y);
        UpdateHover(p);
        var path = HitTest(p).Path;
        _paths[MousePointer] = path;
        var focused = binding.Focus.Primary;
        var e = new PointerEvent(PointerEventKind.Down, MousePointer, p, TimestampMs: (long)binding.Now.TotalMilliseconds);
        Dispatch(path, e);
        Arena.Close(MousePointer);
        // A press outside the focused text field drops its focus (and with it the soft keyboard), unless something else took focus meanwhile.
        if (focused is { OnTextInput: not null } && binding.Focus.Primary == focused && !HitsNode(path, focused)) focused.Unfocus();
        binding.RequestFrame();
    }

    static bool HitsNode(IReadOnlyList<HitTestEntry> path, FocusNode node)
    {
        if (node.Element?.FindRenderObject() is not { } target) return false;
        foreach (var entry in path)
            for (RenderObject? o = entry.Target; o is not null; o = o.Parent)
                if (o == target) return true;
        return false;
    }

    public void PointerMove(float x, float y)
    {
        var p = new Offset(x, y);
        var delta = p - _lastPosition;
        _lastPosition = p;
        long t = (long)binding.Now.TotalMilliseconds;

        if (_paths.TryGetValue(MousePointer, out var path))
        {
            var e = new PointerEvent(PointerEventKind.Move, MousePointer, p, delta, TimestampMs: t);
            Dispatch(path, e);
            Router.Route(e);
        }
        // Handlers that change state request their own frame; only a hover or cursor change needs one here.
        if (UpdateHover(p)) binding.RequestFrame();
    }

    public void PointerUp(float x, float y)
    {
        var p = new Offset(x, y);
        _lastPosition = p;
        if (!_paths.Remove(MousePointer, out var path)) return;
        var e = new PointerEvent(PointerEventKind.Up, MousePointer, p, TimestampMs: (long)binding.Now.TotalMilliseconds);
        Dispatch(path, e);
        Router.Route(e);
        Arena.Sweep(MousePointer);
        UpdateHover(p);
        binding.RequestFrame();
    }

    public void PointerScroll(float x, float y, float dx, float dy)
    {
        var p = new Offset(x, y);
        var path = HitTest(p).Path;
        var e = new PointerEvent(PointerEventKind.Scroll, MousePointer, p, ScrollDelta: new Offset(dx, dy));
        PointerSignal.Reset();
        foreach (var entry in path.ToArray())
        {
            entry.Target.HandlePointerEvent(e with { LocalPosition = entry.ToLocal(p) }, entry);
            if (PointerSignal.Handled) break;
        }
        binding.RequestFrame();
    }

    /// <summary>Re-hit-tests at <paramref name="p"/>; returns true when the hovered regions or the cursor changed.</summary>
    bool UpdateHover(Offset p)
    {
        var path = HitTest(p).Path;
        var regions = new List<RenderMouseRegion>();
        for (int i = 0; i < path.Count; i++)
            if (path[i].Target is RenderMouseRegion region) regions.Add(region);

        bool changed = regions.Count != _hovered.Count;
        for (int i = 0; !changed && i < regions.Count; i++)
            changed = regions[i] != _hovered[i];

        if (changed)
        {
            foreach (var old in _hovered)
                if (!regions.Contains(old)) old.OnExit?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
            foreach (var added in regions)
                if (!_hovered.Contains(added)) added.OnEnter?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
        }
        foreach (var r in regions)
            r.OnHover?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
        _hovered = regions;

        // The innermost region that asks for a cursor decides.
        var cursor = MouseCursor.Default;
        foreach (var r in regions)
            if (r.Cursor != MouseCursor.Default) { cursor = r.Cursor; break; }
        changed |= cursor != Cursor;
        Cursor = cursor;
        return changed;
    }

    /// <summary>Layout can move widgets under a stationary pointer; refresh hover after a frame that laid out.</summary>
    internal void AfterFrame()
    {
        if (binding.RenderView is null) return;
        if (!binding.PipelineOwner.DidLayout) return;
        binding.PipelineOwner.DidLayout = false;
        if (UpdateHover(_lastPosition)) binding.RequestFrame();
    }
}

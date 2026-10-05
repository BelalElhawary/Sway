namespace Sway.Widgets;

public interface IGestureArenaMember
{
    void AcceptGesture(int pointer);
    void RejectGesture(int pointer);
}

/// <summary>
/// Resolves which of several competing recognizers (tap vs drag...) owns a pointer.
/// The first member to claim it wins; if nobody claims, the deepest member wins on pointer-up.
/// </summary>
public sealed class GestureArena
{
    sealed class Entry
    {
        public readonly List<IGestureArenaMember> Members = new();
        public bool Closed;
        public IGestureArenaMember? EagerWinner;
    }

    readonly Dictionary<int, Entry> _arenas = new();

    public void Add(int pointer, IGestureArenaMember member)
    {
        if (!_arenas.TryGetValue(pointer, out var e)) _arenas[pointer] = e = new Entry();
        e.Members.Add(member);
    }

    /// <summary>No more members will join; a lone survivor wins immediately.</summary>
    public void Close(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var e)) return;
        e.Closed = true;
        TryResolveLast(pointer, e);
    }

    /// <summary>Pointer-up with no claimant: the first (deepest) member wins and the rest lose.</summary>
    public void Sweep(int pointer)
    {
        if (!_arenas.Remove(pointer, out var e) || e.Members.Count == 0) return;
        var winner = e.EagerWinner ?? e.Members[0];
        foreach (var m in e.Members.ToArray())
            if (!ReferenceEquals(m, winner)) m.RejectGesture(pointer);
        winner.AcceptGesture(pointer);
    }

    public void Resolve(int pointer, IGestureArenaMember member, bool accepted)
    {
        if (!_arenas.TryGetValue(pointer, out var e) || !e.Members.Contains(member)) return;

        if (!accepted)
        {
            e.Members.Remove(member);
            member.RejectGesture(pointer);
            if (e.Members.Count == 0) _arenas.Remove(pointer);
            else TryResolveLast(pointer, e);
            return;
        }

        _arenas.Remove(pointer);
        foreach (var m in e.Members.ToArray())
            if (!ReferenceEquals(m, member)) m.RejectGesture(pointer);
        member.AcceptGesture(pointer);
    }

    void TryResolveLast(int pointer, Entry e)
    {
        if (!e.Closed || e.Members.Count != 1) return;
        _arenas.Remove(pointer);
        e.Members[0].AcceptGesture(pointer);
    }
}

/// <summary>Delivers raw pointer events by pointer id, independent of where the pointer currently is.</summary>
public sealed class PointerRouter
{
    readonly Dictionary<int, List<Action<PointerEvent>>> _routes = new();

    public void AddRoute(int pointer, Action<PointerEvent> handler)
    {
        if (!_routes.TryGetValue(pointer, out var list)) _routes[pointer] = list = new();
        list.Add(handler);
    }

    public void RemoveRoute(int pointer, Action<PointerEvent> handler)
    {
        if (_routes.TryGetValue(pointer, out var list)) list.Remove(handler);
    }

    public void Route(PointerEvent e)
    {
        if (!_routes.TryGetValue(e.Pointer, out var list)) return;
        foreach (var h in list.ToArray()) h(e);
        if (e.Kind is PointerEventKind.Up or PointerEventKind.Cancel) _routes.Remove(e.Pointer);
    }
}

/// <summary>Hit tests pointer input, dispatches it to render objects, tracks hover state and the cursor.</summary>
public sealed class GestureBinding(WidgetsBinding binding)
{
    const int MousePointer = 0;

    readonly Dictionary<int, IReadOnlyList<HitTestEntry>> _paths = new();
    List<RenderMouseRegion> _hovered = new();
    Offset _lastPosition;
    bool _hoverDirty;

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
        UpdateHover(p);
        binding.RequestFrame();
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

    void UpdateHover(Offset p)
    {
        var result = HitTest(p);
        var regions = result.Path.Select(h => h.Target).OfType<RenderMouseRegion>().ToList();

        foreach (var old in _hovered.Where(r => !regions.Contains(r)))
            old.OnExit?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
        foreach (var added in regions.Where(r => !_hovered.Contains(r)))
            added.OnEnter?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
        foreach (var r in regions)
            r.OnHover?.Invoke(new PointerEvent(PointerEventKind.Hover, MousePointer, p));
        _hovered = regions;

        // The innermost region that asks for a cursor decides.
        Cursor = regions.FirstOrDefault(r => r.Cursor != MouseCursor.Default)?.Cursor ?? MouseCursor.Default;
    }

    /// <summary>Layout can move widgets under a stationary pointer; refresh hover after each frame.</summary>
    internal void AfterFrame()
    {
        if (binding.RenderView is null) return;
        var before = _hovered.ToList();
        UpdateHover(_lastPosition);
        _hoverDirty = !before.SequenceEqual(_hovered);
        if (_hoverDirty) binding.RequestFrame();
    }
}

/// <summary>Lets the innermost scrollable consume a wheel event so outer ones do not also scroll.</summary>
public static class PointerSignal
{
    public static bool Handled { get; private set; }
    public static void Reset() => Handled = false;
    public static void Consume() => Handled = true;
}

public readonly record struct DragDetails(Offset GlobalPosition, Offset LocalPosition, Offset Delta, Offset Velocity = default);

public abstract class GestureRecognizer : IGestureArenaMember
{
    protected static GestureBinding Gestures => WidgetsBinding.Instance.Gestures;

    public abstract void AddPointer(PointerEvent down);
    public abstract void AcceptGesture(int pointer);
    public abstract void RejectGesture(int pointer);
    public virtual void Dispose() { }
}

public sealed class TapGestureRecognizer : GestureRecognizer
{
    const float Slop = 18;

    static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(300);

    public Action<DragDetails>? OnTapDown, OnTapUp;
    public Action? OnTap, OnTapCancel, OnDoubleTap;

    int _pointer = -1;
    Action? _deferredTap;
    TimeSpan _firstTapAt;
    Offset _firstTapPosition;
    Offset _start;
    bool _won, _sawUp, _tapDownFired;
    PointerEvent? _up;

    public override void AddPointer(PointerEvent down)
    {
        if (_pointer >= 0) return;
        _pointer = down.Pointer;
        _start = down.Position;
        _won = _sawUp = false;
        Gestures.Router.AddRoute(_pointer, Handle);
        Gestures.Arena.Add(_pointer, this);
        _tapDownFired = true;
        OnTapDown?.Invoke(new DragDetails(down.Position, down.LocalPosition, default));
    }

    void Handle(PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Move:
                if ((e.Position - _start).Distance > Slop) Gestures.Arena.Resolve(_pointer, this, false);
                break;
            case PointerEventKind.Up:
                _sawUp = true;
                _up = e;
                if (_won) Fire();
                break;
            case PointerEventKind.Cancel:
                Gestures.Arena.Resolve(_pointer, this, false);
                break;
        }
    }

    public override void AcceptGesture(int pointer)
    {
        _won = true;
        if (_sawUp) Fire();
    }

    public override void RejectGesture(int pointer)
    {
        if (_tapDownFired && !(_won && _sawUp)) OnTapCancel?.Invoke();
        Reset();
    }

    void Fire()
    {
        var up = _up!;
        OnTapUp?.Invoke(new DragDetails(up.Position, up.LocalPosition, default));
        Reset();
        RegisterTap(up.Position);
    }

    // With a double-tap handler the single tap waits out the double-tap window, like Flutter's GestureDetector.
    void RegisterTap(Offset position)
    {
        if (OnDoubleTap is null)
        {
            OnTap?.Invoke();
            return;
        }

        var binding = WidgetsBinding.Instance;
        if (_deferredTap is not null && binding.Now - _firstTapAt <= DoubleTapWindow
            && (position - _firstTapPosition).Distance <= Slop * 2)
        {
            binding.CancelTimer(_deferredTap);
            _deferredTap = null;
            OnDoubleTap.Invoke();
            return;
        }

        if (_deferredTap is not null) binding.CancelTimer(_deferredTap);
        _firstTapAt = binding.Now;
        _firstTapPosition = position;
        _deferredTap = () =>
        {
            _deferredTap = null;
            OnTap?.Invoke();
        };
        binding.ScheduleTimer(DoubleTapWindow, _deferredTap);
    }

    void Reset()
    {
        if (_pointer >= 0) Gestures.Router.RemoveRoute(_pointer, Handle);
        _pointer = -1;
        _won = _sawUp = _tapDownFired = false;
    }
}

/// <summary>Fires once the pointer has been held in place for <see cref="Delay"/>.</summary>
public sealed class LongPressGestureRecognizer : GestureRecognizer
{
    const float Slop = 18;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    public Action<DragDetails>? OnLongPressStart, OnLongPressEnd;
    public Action? OnLongPress;

    readonly Action _onTimer;
    int _pointer = -1;
    Offset _start;
    PointerEvent? _down;
    // The arena can be won before the delay passes (lone recognizer); the press only fires once both have happened.
    bool _won, _deadline, _fired;

    public LongPressGestureRecognizer() => _onTimer = OnTimer;

    public override void AddPointer(PointerEvent down)
    {
        if (_pointer >= 0) return;
        _pointer = down.Pointer;
        _start = down.Position;
        _down = down;
        _won = _deadline = _fired = false;
        Gestures.Router.AddRoute(_pointer, Handle);
        Gestures.Arena.Add(_pointer, this);
        WidgetsBinding.Instance.ScheduleTimer(Delay, _onTimer);
    }

    void OnTimer()
    {
        if (_pointer < 0) return;
        _deadline = true;
        if (_won) Fire();
        else Gestures.Arena.Resolve(_pointer, this, true);
    }

    void Fire()
    {
        _fired = true;
        OnLongPressStart?.Invoke(new DragDetails(_down!.Position, _down.LocalPosition, default));
        OnLongPress?.Invoke();
    }

    void Handle(PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Move:
                if (!_fired && (e.Position - _start).Distance > Slop) Abandon();
                break;
            case PointerEventKind.Up:
            case PointerEventKind.Cancel:
                if (_fired && e.Kind == PointerEventKind.Up) OnLongPressEnd?.Invoke(new DragDetails(e.Position, e.LocalPosition, default));
                if (_fired) Reset();
                else Abandon();
                break;
        }
    }

    void Abandon()
    {
        if (_won) Reset();
        else Gestures.Arena.Resolve(_pointer, this, false);
    }

    public override void AcceptGesture(int pointer)
    {
        _won = true;
        if (_deadline) Fire();
    }

    public override void RejectGesture(int pointer)
    {
        if (!_fired) Reset();
    }

    void Reset()
    {
        if (_pointer >= 0) Gestures.Router.RemoveRoute(_pointer, Handle);
        WidgetsBinding.Instance.CancelTimer(_onTimer);
        _pointer = -1;
        _won = _deadline = _fired = false;
        _down = null;
    }
}

public enum DragAxis { Both, Vertical, Horizontal }

/// <summary>Recognizes drags along an axis (or any direction) once the pointer moves past a small slop.</summary>
public sealed class DragGestureRecognizer(DragAxis axis) : GestureRecognizer
{
    const float Slop = 6;

    public Action<DragDetails>? OnDown, OnStart, OnUpdate, OnEnd;
    public Action? OnCancel;

    int _pointer = -1;
    Offset _start, _last, _pending;
    bool _accepted;
    readonly List<(long t, Offset p)> _samples = new();

    public override void AddPointer(PointerEvent down)
    {
        if (_pointer >= 0) return;
        _pointer = down.Pointer;
        _start = _last = down.Position;
        _pending = Offset.Zero;
        _accepted = false;
        _samples.Clear();
        _samples.Add((down.TimestampMs, down.Position));
        Gestures.Router.AddRoute(_pointer, Handle);
        Gestures.Arena.Add(_pointer, this);
        OnDown?.Invoke(new DragDetails(down.Position, down.LocalPosition, default));
    }

    Offset Constrain(Offset d) => axis switch { DragAxis.Vertical => new(0, d.Dy), DragAxis.Horizontal => new(d.Dx, 0), _ => d };

    void Handle(PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Move:
                _samples.Add((e.TimestampMs, e.Position));
                if (_samples.Count > 20) _samples.RemoveAt(0);
                var delta = Constrain(e.Position - _last);
                _last = e.Position;
                if (_accepted)
                {
                    OnUpdate?.Invoke(new DragDetails(e.Position, e.LocalPosition, delta));
                }
                else
                {
                    _pending += delta;
                    if (_pending.Distance > Slop) Gestures.Arena.Resolve(_pointer, this, true);
                }
                break;
            case PointerEventKind.Up:
                if (_accepted) OnEnd?.Invoke(new DragDetails(e.Position, e.LocalPosition, default, Velocity()));
                else Gestures.Arena.Resolve(_pointer, this, false);
                Reset();
                break;
            case PointerEventKind.Cancel:
                if (_accepted) OnCancel?.Invoke();
                else Gestures.Arena.Resolve(_pointer, this, false);
                Reset();
                break;
        }
    }

    Offset Velocity()
    {
        // Pixels per second over the last ~100ms of movement.
        if (_samples.Count < 2) return Offset.Zero;
        var last = _samples[^1];
        var first = _samples.LastOrDefault(s => last.t - s.t >= 80, _samples[0]);
        float dt = Math.Max(1, last.t - first.t) / 1000f;
        return Constrain(new Offset((last.p.Dx - first.p.Dx) / dt, (last.p.Dy - first.p.Dy) / dt));
    }

    public override void AcceptGesture(int pointer)
    {
        _accepted = true;
        var p = _start + _pending;
        OnStart?.Invoke(new DragDetails(_start, _start, default));
        if (_pending != Offset.Zero) OnUpdate?.Invoke(new DragDetails(p, p, _pending));
        _pending = Offset.Zero;
    }

    public override void RejectGesture(int pointer)
    {
        if (!_accepted) Reset();
    }

    void Reset()
    {
        if (_pointer >= 0) Gestures.Router.RemoveRoute(_pointer, Handle);
        _pointer = -1;
        _accepted = false;
    }
}

public sealed class Listener(Widget? child = null, Action<PointerEvent>? onPointerDown = null, Action<PointerEvent>? onPointerMove = null,
    Action<PointerEvent>? onPointerUp = null, Action<PointerEvent>? onPointerCancel = null, Action<PointerEvent>? onPointerScroll = null,
    HitTestBehavior behavior = HitTestBehavior.DeferToChild, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderPointerListener();
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => Apply((RenderPointerListener)ro);

    void Apply(RenderPointerListener r)
    {
        r.OnPointerDown = onPointerDown; r.OnPointerMove = onPointerMove; r.OnPointerUp = onPointerUp;
        r.OnPointerCancel = onPointerCancel; r.OnPointerScroll = onPointerScroll; r.Behavior = behavior;
    }
}

public sealed class MouseRegion(Widget? child = null, Action<PointerEvent>? onEnter = null, Action<PointerEvent>? onExit = null,
    Action<PointerEvent>? onHover = null, MouseCursor cursor = MouseCursor.Default, bool opaque = true, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderMouseRegion();
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => Apply((RenderMouseRegion)ro);

    void Apply(RenderMouseRegion r)
    {
        r.OnEnter = onEnter; r.OnExit = onExit; r.OnHover = onHover; r.Cursor = cursor; r.Opaque = opaque;
    }
}

/// <summary>Detects taps and drags on its child.</summary>
public sealed class GestureDetector(Widget? child = null, Action? onTap = null, Action<DragDetails>? onTapDown = null,
    Action<DragDetails>? onTapUp = null, Action? onTapCancel = null,
    Action<DragDetails>? onPanStart = null, Action<DragDetails>? onPanUpdate = null, Action<DragDetails>? onPanEnd = null,
    Action<DragDetails>? onVerticalDragStart = null, Action<DragDetails>? onVerticalDragUpdate = null, Action<DragDetails>? onVerticalDragEnd = null,
    Action<DragDetails>? onHorizontalDragStart = null, Action<DragDetails>? onHorizontalDragUpdate = null, Action<DragDetails>? onHorizontalDragEnd = null,
    Action? onDoubleTap = null, Action? onLongPress = null, Action<DragDetails>? onLongPressStart = null, Action<DragDetails>? onLongPressEnd = null,
    HitTestBehavior? behavior = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget? Child => child;
    internal Action? OnTap => onTap;
    internal Action<DragDetails>? OnTapDown => onTapDown;
    internal Action<DragDetails>? OnTapUp => onTapUp;
    internal Action? OnTapCancel => onTapCancel;
    internal Action? OnDoubleTap => onDoubleTap;
    internal (Action? press, Action<DragDetails>? start, Action<DragDetails>? end) LongPress => (onLongPress, onLongPressStart, onLongPressEnd);
    internal (Action<DragDetails>? start, Action<DragDetails>? update, Action<DragDetails>? end) Pan => (onPanStart, onPanUpdate, onPanEnd);
    internal (Action<DragDetails>? start, Action<DragDetails>? update, Action<DragDetails>? end) Vertical => (onVerticalDragStart, onVerticalDragUpdate, onVerticalDragEnd);
    internal (Action<DragDetails>? start, Action<DragDetails>? update, Action<DragDetails>? end) Horizontal => (onHorizontalDragStart, onHorizontalDragUpdate, onHorizontalDragEnd);
    internal HitTestBehavior? Behavior => behavior;

    public override State CreateState() => new GestureDetectorState();
}

sealed class GestureDetectorState : State<GestureDetector>
{
    TapGestureRecognizer? _tap;
    LongPressGestureRecognizer? _longPress;
    DragGestureRecognizer? _pan, _vertical, _horizontal;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(GestureDetector old) => Sync();

    void Sync()
    {
        var w = Widget;
        if (w.OnTap is not null || w.OnTapDown is not null || w.OnTapUp is not null || w.OnTapCancel is not null || w.OnDoubleTap is not null)
            (_tap ??= new TapGestureRecognizer()).Apply(w);
        else _tap = null;

        var lp = w.LongPress;
        if (lp.press is not null || lp.start is not null || lp.end is not null)
        {
            _longPress ??= new LongPressGestureRecognizer();
            _longPress.OnLongPress = lp.press; _longPress.OnLongPressStart = lp.start; _longPress.OnLongPressEnd = lp.end;
        }
        else _longPress = null;
        _pan = SyncDrag(_pan, DragAxis.Both, w.Pan);
        _vertical = SyncDrag(_vertical, DragAxis.Vertical, w.Vertical);
        _horizontal = SyncDrag(_horizontal, DragAxis.Horizontal, w.Horizontal);
    }

    static DragGestureRecognizer? SyncDrag(DragGestureRecognizer? r, DragAxis axis,
        (Action<DragDetails>? start, Action<DragDetails>? update, Action<DragDetails>? end) cb)
    {
        if (cb.start is null && cb.update is null && cb.end is null) return null;
        r ??= new DragGestureRecognizer(axis);
        r.OnStart = cb.start; r.OnUpdate = cb.update; r.OnEnd = cb.end;
        return r;
    }

    void OnPointerDown(PointerEvent e)
    {
        _tap?.AddPointer(e);
        _longPress?.AddPointer(e);
        _pan?.AddPointer(e);
        _vertical?.AddPointer(e);
        _horizontal?.AddPointer(e);
    }

    public override Widget Build(BuildContext context) =>
        new Listener(Widget.Child, onPointerDown: OnPointerDown,
            behavior: Widget.Behavior ?? (Widget.Child is null ? HitTestBehavior.Opaque : HitTestBehavior.DeferToChild));
}

static class TapRecognizerExt
{
    public static void Apply(this TapGestureRecognizer r, GestureDetector w)
    {
        r.OnDoubleTap = w.OnDoubleTap; r.OnTap = w.OnTap; r.OnTapDown = w.OnTapDown; r.OnTapUp = w.OnTapUp; r.OnTapCancel = w.OnTapCancel;
    }
}

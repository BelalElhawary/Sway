namespace Sway.Widgets;

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

namespace Sway.Widgets;

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

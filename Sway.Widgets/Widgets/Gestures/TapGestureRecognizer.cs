namespace Sway.Widgets;

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

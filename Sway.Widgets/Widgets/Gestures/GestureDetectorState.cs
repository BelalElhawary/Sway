namespace Sway.Widgets;

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

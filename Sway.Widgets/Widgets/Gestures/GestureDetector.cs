namespace Sway.Widgets;

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

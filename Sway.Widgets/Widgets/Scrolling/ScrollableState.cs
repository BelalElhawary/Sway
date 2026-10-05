using SkiaSharp;

namespace Sway.Widgets;

sealed class ScrollableState : State<Scrollable>
{
    ScrollController? _owned;
    ScrollPosition Position => (Widget.Controller ?? (_owned ??= new ScrollController())).Position;

    void OnWheel(PointerEvent e)
    {
        float delta = Widget.Axis == Axis.Vertical
            ? (e.ScrollDelta.Dy != 0 || !Widget.WheelScrollsOtherAxis ? e.ScrollDelta.Dy : e.ScrollDelta.Dx)
            : (e.ScrollDelta.Dx != 0 || !Widget.WheelScrollsOtherAxis ? e.ScrollDelta.Dx : e.ScrollDelta.Dy);
        // Only consume the wheel if this scrollable can move, so an outer one can take over at the edge.
        if (Position.ScrollBy(delta)) PointerSignal.Consume();
    }

    // The scrollbar can own the pointer while this drag recognizer (which may win the arena at pointer-down) is
    // also tracking it; remember per drag so the scrollbar's own release cannot let the content fling.
    bool _scrollbarDrag;

    void DragStart(DragDetails _)
    {
        _scrollbarDrag = Position.ScrollbarActive;
        if (!_scrollbarDrag) Position.StopActivity();
    }

    void DragUpdate(DragDetails d)
    {
        if (_scrollbarDrag) return;
        Position.JumpTo(Position.Pixels - (Widget.Axis == Axis.Vertical ? d.Delta.Dy : d.Delta.Dx));
    }

    void DragEnd(DragDetails d)
    {
        bool ignore = _scrollbarDrag;
        _scrollbarDrag = false;
        if (!ignore) Position.Fling(-(Widget.Axis == Axis.Vertical ? d.Velocity.Dy : d.Velocity.Dx));
    }

    public override Widget Build(BuildContext context)
    {
        bool v = Widget.Axis == Axis.Vertical;
        return new Listener(onPointerScroll: OnWheel, behavior: HitTestBehavior.Translucent,
            child: new GestureDetector(
                onVerticalDragStart: v ? DragStart : null, onVerticalDragUpdate: v ? DragUpdate : null, onVerticalDragEnd: v ? DragEnd : null,
                onHorizontalDragStart: v ? null : DragStart, onHorizontalDragUpdate: v ? null : DragUpdate, onHorizontalDragEnd: v ? null : DragEnd,
                behavior: HitTestBehavior.Translucent,
                child: Widget.ViewportBuilder(context, Position)));
    }
}

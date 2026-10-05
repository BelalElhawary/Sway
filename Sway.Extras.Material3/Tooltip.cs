using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Shows a short label near its child after the pointer rests on it for <c>waitDuration</c>.</summary>
public sealed class Tooltip(string message, Widget child, TimeSpan? waitDuration = null, Key? key = null) : StatefulWidget(key)
{
    internal string Message => message;
    internal Widget Child => child;
    internal TimeSpan Wait => waitDuration ?? TimeSpan.FromMilliseconds(500);
    public override State CreateState() => new TooltipState();
}

sealed class TooltipState : State<Tooltip>
{
    OverlayEntry? _entry;
    Action? _showTimer;

    public override void Dispose() => Hide();

    void ScheduleShow()
    {
        Hide();
        _showTimer = Show;
        WidgetsBinding.Instance.ScheduleTimer(Widget.Wait, _showTimer);
    }

    void Show()
    {
        _showTimer = null;
        if (_entry is not null || !Mounted || Context.FindRenderObject() is not RenderBox box || box.SizeOrNull is not { } size) return;
        var overlay = Overlay.MaybeOf(Context);
        if (overlay is null) return;

        var origin = box.LocalToGlobal(Offset.Zero);
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        bool above = origin.Dy + size.Height + 40 > window.Height && origin.Dy > 40;
        var owner = Context;
        string message = Widget.Message;

        _entry = new OverlayEntry(ctx =>
        {
            var theme = Theme.Of(owner);
            var s = theme.ColorScheme;
            Widget bubble = new DecoratedBox(new BoxDecoration(Color: s.InverseSurface, BorderRadius: BorderRadius.Circular(Shapes.ExtraSmall)),
                new Padding(EdgeInsets.Symmetric(8, 4), new Text(message, style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnInverseSurface)))));
            return Dialogs.Wrap(owner, new Stack([
                new Positioned(new IgnorePointer(new OverflowBox(bubble, alignment: above ? Alignment.BottomCenter : Alignment.TopCenter,
                    minWidth: 0, maxWidth: 320, minHeight: 0, maxHeight: 200)),
                    left: origin.Dx, width: size.Width,
                    top: above ? origin.Dy - 8 - 200 : origin.Dy + size.Height + 8, height: 200),
            ], fit: StackFit.Expand, clip: false));
        });
        overlay.Insert(_entry);
    }

    void Hide()
    {
        if (_showTimer is not null) WidgetsBinding.Instance.CancelTimer(_showTimer);
        _showTimer = null;
        _entry?.Remove();
        _entry = null;
    }

    public override Widget Build(BuildContext context) =>
        new MouseRegion(onEnter: _ => ScheduleShow(), onExit: _ => Hide(), opaque: false,
            child: new Listener(Widget.Child, onPointerDown: _ => Hide(), behavior: HitTestBehavior.Translucent));
}

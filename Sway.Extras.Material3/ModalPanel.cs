using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

enum PanelEdge { Start, End, Bottom }

sealed class ModalPanel(PanelEdge edge, float? width, bool dismissible, Func<BuildContext, Action, Widget> builder, Action onRemoved,
    Action<Action> registerClose) : StatefulWidget
{
    internal PanelEdge Edge => edge;
    internal float? Width => width;
    internal bool Dismissible => dismissible;
    internal Func<BuildContext, Action, Widget> Builder => builder;
    internal Action OnRemoved => onRemoved;
    internal Action<Action> RegisterClose => registerClose;
    public override State CreateState() => new ModalPanelState();
}

sealed class ModalPanelState : TickerProviderState<ModalPanel>
{
    AnimationController _controller = null!;
    CurvedAnimation _curved = null!;
    float _drag;
    bool _closing;

    public override void InitState()
    {
        _controller = new AnimationController(this, TimeSpan.FromMilliseconds(260));
        _curved = new CurvedAnimation(_controller, Curves.EaseOutCubic, Curves.EaseInCubic);
        _controller.AddStatusListener(status =>
        {
            if (status == AnimationStatus.Dismissed && _closing) Widget.OnRemoved();
        });
        Widget.RegisterClose(Close);
        _controller.Forward();
    }

    public override void Dispose()
    {
        _curved.Dispose();
        _controller.Dispose();
        base.Dispose();
    }

    void Close()
    {
        if (_closing) return;
        _closing = true;
        _controller.Reverse();
    }

    void DismissIfAllowed()
    {
        if (Widget.Dismissible) Close();
    }

    bool OnKey(KeyEvent e)
    {
        if (e.IsDown && e.Key == "Escape" && Widget.Dismissible)
        {
            Close();
            return true;
        }
        return false;
    }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        bool bottom = Widget.Edge == PanelEdge.Bottom;
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        // Start means the left in left-to-right text and the right in right-to-left text.
        bool left = Widget.Edge == PanelEdge.Start != rtl;

        Widget panel = Widget.Builder(context, Close);
        if (bottom)
        {
            panel = new GestureDetector(
                onVerticalDragUpdate: d => SetState(() => _drag = Math.Max(0, _drag + d.Delta.Dy)),
                onVerticalDragEnd: d =>
                {
                    bool dismiss = Widget.Dismissible && (_drag > 120 || d.Velocity.Dy > 800);
                    SetState(() => _drag = 0);
                    if (dismiss) Close();
                },
                child: panel);
        }

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: OnKey,
            child: new AnimatedBuilder(_curved, (ctx, _) =>
            {
                float t = _curved.Value;
                Widget slid = bottom
                    ? new FractionalTranslation(new Offset(0, 1 - t), new Transform(SKMatrix.CreateTranslation(0, _drag), panel))
                    : new FractionalTranslation(new Offset((left ? -1 : 1) * (1 - t), 0), panel);
                return new Stack([
                    Positioned.Fill(new GestureDetector(onTap: DismissIfAllowed, behavior: HitTestBehavior.Opaque,
                        child: new ColoredBox(s.Scrim.WithOpacity(0.32f * t)))),
                    Positioned.Fill(new Align(bottom ? Alignment.BottomCenter : left ? Alignment.CenterLeft : Alignment.CenterRight,
                        bottom ? slid : new SizedBox(width: Widget.Width, child: slid))),
                ], fit: StackFit.Expand, clip: false);
            }));
    }
}

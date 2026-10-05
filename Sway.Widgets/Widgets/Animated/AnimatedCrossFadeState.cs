using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedCrossFadeState : TickerProviderState<AnimatedCrossFade>
{
    AnimationController _controller = null!;
    CurvedAnimation _animation = null!;

    public override void InitState()
    {
        _controller = new AnimationController(this, Widget.Duration, value: Widget.State == CrossFadeState.ShowSecond ? 1 : 0);
        _animation = new CurvedAnimation(_controller, Widget.Curve);
    }

    public override void DidUpdateWidget(AnimatedCrossFade old)
    {
        _controller.Duration = Widget.Duration;
        _animation.Curve = Widget.Curve;
        if (old.State == Widget.State) return;
        if (Widget.State == CrossFadeState.ShowSecond) _controller.Forward(); else _controller.Reverse();
    }

    public override void Dispose()
    {
        _animation.Dispose();
        _controller.Dispose();
        base.Dispose();
    }

    public override Widget Build(BuildContext context)
    {
        bool second = Widget.State == CrossFadeState.ShowSecond;
        Widget first = new IgnorePointer(new FadeTransition(new Reversed(_animation), Widget.First), ignoring: second);
        Widget secondW = new IgnorePointer(new FadeTransition(_animation, Widget.Second), ignoring: !second);

        // The visible child takes part in layout; the hiding one is overlaid at the same width.
        Widget hidden(Widget w) => new Positioned(w, left: 0, top: 0, right: 0);
        return new AnimatedSize(Widget.Duration, new Stack(
            second ? [hidden(first), secondW] : [hidden(secondW), first],
            alignment: Alignment.TopCenter, clip: false), curve: Widget.Curve);
    }
}

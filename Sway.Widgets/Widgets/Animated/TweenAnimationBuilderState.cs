using SkiaSharp;

namespace Sway.Widgets;

sealed class TweenAnimationBuilderState<T> : TickerProviderState<TweenAnimationBuilder<T>> where T : notnull
{
    AnimationController _controller = null!;
    CurvedAnimation _curved = null!;
    T _from = default!, _to = default!;

    public override void InitState()
    {
        _controller = new AnimationController(this, Widget.Duration);
        _controller.AddListener(() => { if (Mounted) SetState(); });
        _controller.AddStatusListener(s => { if (s == AnimationStatus.Completed) Widget.OnEnd?.Invoke(); });
        _curved = new CurvedAnimation(_controller, Widget.Curve);
        _from = Widget.Tween.Begin;
        _to = Widget.Tween.End;
        if (!EqualityComparer<T>.Default.Equals(_from, _to)) _controller.Forward();
        else _controller.SetValue(1);
    }

    public override void DidUpdateWidget(TweenAnimationBuilder<T> old)
    {
        _controller.Duration = Widget.Duration;
        _curved.Curve = Widget.Curve;
        if (EqualityComparer<T>.Default.Equals(_to, Widget.Tween.End)) return;
        // Start the new run from the value currently on screen.
        _from = Evaluate(_curved.Value);
        _to = Widget.Tween.End;
        _controller.SetValue(0);
        _controller.Forward();
    }

    // The user's tween supplies only the blend function, so one tween object can safely back several builders.
    T Evaluate(float t) => Widget.Tween.Interpolate(_from, _to, t);

    public override void Dispose()
    {
        _curved.Dispose();
        _controller.Dispose();
        base.Dispose();
    }

    public override Widget Build(BuildContext context) => Widget.Builder(context, Evaluate(_curved.Value), Widget.Child);
}

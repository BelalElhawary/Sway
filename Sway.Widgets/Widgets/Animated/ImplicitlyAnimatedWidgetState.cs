using SkiaSharp;

namespace Sway.Widgets;

public abstract class ImplicitlyAnimatedWidgetState<TWidget> : TickerProviderState<TWidget> where TWidget : ImplicitlyAnimatedWidget
{
    AnimationController _controller = null!;
    CurvedAnimation _curved = null!;

    protected Animation<float> Animation => _curved;

    /// <summary>Visit each animated property: <c>_color = v.VisitValue(_color, Widget.Color, c => new ColorTween(c))</c>.</summary>
    protected abstract void ForEachTween(ITweenVisitor visitor);

    public override void InitState()
    {
        _controller = new AnimationController(this, Widget.Duration);
        _controller.AddListener(() => { if (Mounted) SetState(); });
        _controller.AddStatusListener(s => { if (s == AnimationStatus.Completed) Widget.OnEnd?.Invoke(); });
        _curved = new CurvedAnimation(_controller, Widget.Curve);
        ForEachTween(new InitVisitor());
    }

    public override void DidUpdateWidget(TWidget old)
    {
        _controller.Duration = Widget.Duration;
        _curved.Curve = Widget.Curve;
        RetargetTweens();
    }

    /// <summary>Re-reads the animated properties and animates from the current values toward any that changed.</summary>
    protected void RetargetTweens()
    {
        var visitor = new UpdateVisitor(_curved);
        ForEachTween(visitor);
        if (visitor.Changed)
        {
            _controller.SetValue(0);
            _controller.Forward();
        }
    }

    // Properties that depend on inherited widgets (text direction) are re-resolved when those change.
    public override void DidChangeDependencies()
    {
        base.DidChangeDependencies();
        RetargetTweens();
    }

    public override void Dispose()
    {
        _curved.Dispose();
        _controller.Dispose();
        base.Dispose();
    }

    sealed class InitVisitor : ITweenVisitor
    {
        public Tween<T>? Visit<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : class => target is null ? null : Hold(create, target);
        public Tween<T>? VisitValue<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : struct => target is null ? null : Hold(create, target.Value);

        static Tween<T> Hold<T>(Func<T, Tween<T>> create, T target) where T : notnull
        {
            var t = create(target);
            t.Begin = t.End = target;
            return t;
        }
    }

    sealed class UpdateVisitor(Animation<float> animation) : ITweenVisitor
    {
        public bool Changed;

        public Tween<T>? Visit<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : class =>
            target is null ? Drop(tween) : Retarget(tween, target, create);

        public Tween<T>? VisitValue<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : struct =>
            target is null ? Drop(tween) : Retarget(tween, target.Value, create);

        Tween<T>? Drop<T>(Tween<T>? tween) where T : notnull
        {
            if (tween is not null) Changed = true;
            return null;
        }

        Tween<T> Retarget<T>(Tween<T>? tween, T target, Func<T, Tween<T>> create) where T : notnull
        {
            if (tween is null)
            {
                tween = create(target);
                tween.Begin = tween.End = target;
                return tween;
            }
            if (!EqualityComparer<T>.Default.Equals(tween.End, target))
            {
                // Restart from wherever the old animation had reached, so retargeting mid-flight is smooth.
                tween.Begin = tween.Transform(animation.Value);
                tween.End = target;
                Changed = true;
            }
            return tween;
        }
    }
}

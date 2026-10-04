using SkiaSharp;

namespace Sway.Widgets;

// ---- explicit animation widgets ----

/// <summary>Rebuilds <paramref name="builder"/> whenever <paramref name="animation"/> notifies. Pass static subtrees as <c>child</c>.</summary>
public sealed class AnimatedBuilder(IListenable animation, Func<BuildContext, Widget?, Widget> builder, Widget? child = null, Key? key = null) : StatefulWidget(key)
{
    internal IListenable Animation => animation;
    internal Func<BuildContext, Widget?, Widget> Builder => builder;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedBuilderState();
}

sealed class AnimatedBuilderState : State<AnimatedBuilder>
{
    public override void InitState() => Widget.Animation.AddListener(OnChanged);

    public override void DidUpdateWidget(AnimatedBuilder old)
    {
        if (ReferenceEquals(old.Animation, Widget.Animation)) return;
        old.Animation.RemoveListener(OnChanged);
        Widget.Animation.AddListener(OnChanged);
    }

    public override void Dispose() => Widget.Animation.RemoveListener(OnChanged);

    void OnChanged() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context) => Widget.Builder(context, Widget.Child);
}

public sealed class FadeTransition(Animation<float> opacity, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(opacity, (_, c) => new Opacity(opacity.Value, c), child);
}

public sealed class ScaleTransition(Animation<float> scale, Widget? child = null, Alignment? alignment = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(scale, (_, c) => Transform.Scale(scale.Value, c, alignment), child);
}

public sealed class RotationTransition(Animation<float> turns, Widget? child = null, Alignment? alignment = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(turns, (_, c) => Transform.Rotate(turns.Value * MathF.PI * 2, c, alignment), child);
}

/// <summary>Slides its child by a fraction of its own size (1.0 = one full width/height).</summary>
public sealed class SlideTransition(Animation<Offset> position, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(position, (_, c) => new FractionalTranslation(position.Value, c), child);
}

public sealed class DecoratedBoxTransition(Animation<BoxDecoration> decoration, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedBuilder(decoration, (_, c) => new DecoratedBox(decoration.Value, c), child);
}

public sealed class FractionalTranslation(Offset translation, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFractionalTranslation(translation);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFractionalTranslation)ro).Translation = translation;
}

sealed class RenderFractionalTranslation(Offset translation) : RenderProxyBox
{
    Offset _translation = translation;
    public Offset Translation { set { if (_translation == value) return; _translation = value; MarkNeedsPaint(); } }

    Offset Shift => new(_translation.Dx * Size.Width, _translation.Dy * Size.Height);

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c) context.PaintChild(c, offset + Shift);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithPaintOffset(Shift, position, c.HitTest);
}

// ---- implicit animations ----

public interface ITweenVisitor
{
    Tween<T>? Visit<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : class;
    Tween<T>? VisitValue<T>(Tween<T>? tween, T? target, Func<T, Tween<T>> create) where T : struct;
}

/// <summary>A widget that animates its properties to new values whenever it is rebuilt with them.</summary>
public abstract class ImplicitlyAnimatedWidget(TimeSpan duration, Curve? curve = null, Action? onEnd = null, Key? key = null) : StatefulWidget(key)
{
    public TimeSpan Duration { get; } = duration;
    public Curve Curve { get; } = curve ?? Curves.Linear;
    public Action? OnEnd { get; } = onEnd;
}

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

public sealed class AnimatedContainer(TimeSpan duration, Widget? child = null, IAlignment? alignment = null, EdgeInsets? padding = null,
    SKColor? color = null, BoxDecoration? decoration = null, BoxDecoration? foregroundDecoration = null, float? width = null,
    float? height = null, BoxConstraints? constraints = null, EdgeInsets? margin = null, SKMatrix? transform = null,
    Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal IAlignment? Alignment => alignment;
    internal EdgeInsets? Padding => padding;
    internal BoxDecoration? Decoration => decoration ?? (color is { } c ? new BoxDecoration(Color: c) : null);
    internal BoxDecoration? Foreground => foregroundDecoration;
    internal BoxConstraints? Constraints =>
        width is null && height is null ? constraints
            : (constraints ?? new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity)).Tighten(width, height);
    internal EdgeInsets? Margin => margin;
    internal SKMatrix? TransformMatrix => transform;

    public override State CreateState() => new AnimatedContainerState();
}

sealed class AnimatedContainerState : ImplicitlyAnimatedWidgetState<AnimatedContainer>
{
    Tween<Alignment>? _alignment;
    Tween<EdgeInsets>? _padding, _margin;
    Tween<BoxDecoration>? _decoration, _foreground;
    Tween<BoxConstraints>? _constraints;
    Tween<SKMatrix>? _transform;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _alignment = v.VisitValue(_alignment, Widget.Alignment?.Resolve(Directionality.Of(Context)), a => new AlignmentTween(a, a));
        _padding = v.VisitValue(_padding, Widget.Padding, a => new EdgeInsetsTween(a, a));
        _decoration = v.Visit(_decoration, Widget.Decoration, a => new DecorationTween(a, a));
        _foreground = v.Visit(_foreground, Widget.Foreground, a => new DecorationTween(a, a));
        _constraints = v.VisitValue(_constraints, Widget.Constraints, a => new BoxConstraintsTween(a, a));
        _margin = v.VisitValue(_margin, Widget.Margin, a => new EdgeInsetsTween(a, a));
        _transform = v.VisitValue(_transform, Widget.TransformMatrix, a => new MatrixTween(a, a));
    }

    public override Widget Build(BuildContext context) => new Container(
        Widget.Child,
        alignment: _alignment?.Evaluate(Animation),
        padding: _padding?.Evaluate(Animation),
        decoration: _decoration?.Evaluate(Animation),
        foregroundDecoration: _foreground?.Evaluate(Animation),
        constraints: _constraints?.Evaluate(Animation),
        margin: _margin?.Evaluate(Animation),
        transform: _transform?.Evaluate(Animation));
}

public sealed class AnimatedOpacity(float opacity, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => opacity;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedOpacityState();
}

sealed class AnimatedOpacityState : ImplicitlyAnimatedWidgetState<AnimatedOpacity>
{
    Tween<float>? _opacity;
    protected override void ForEachTween(ITweenVisitor v) => _opacity = v.VisitValue(_opacity, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => new Opacity(_opacity!.Evaluate(Animation), Widget.Child);
}

public sealed class AnimatedPadding(EdgeInsets padding, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal EdgeInsets Target => padding;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedPaddingState();
}

sealed class AnimatedPaddingState : ImplicitlyAnimatedWidgetState<AnimatedPadding>
{
    Tween<EdgeInsets>? _padding;
    protected override void ForEachTween(ITweenVisitor v) => _padding = v.VisitValue(_padding, Widget.Target, a => new EdgeInsetsTween(a, a));
    public override Widget Build(BuildContext context) => new Padding(_padding!.Evaluate(Animation), Widget.Child);
}

public sealed class AnimatedAlign(IAlignment alignment, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal IAlignment Target => alignment;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedAlignState();
}

sealed class AnimatedAlignState : ImplicitlyAnimatedWidgetState<AnimatedAlign>
{
    Tween<Alignment>? _alignment;
    protected override void ForEachTween(ITweenVisitor v) =>
        _alignment = v.VisitValue(_alignment, (Alignment?)Widget.Target.Resolve(Directionality.Of(Context)), a => new AlignmentTween(a, a));
    public override Widget Build(BuildContext context) => new Align(_alignment!.Evaluate(Animation), Widget.Child);
}

/// <summary>An <see cref="AnimatedPositioned"/> whose horizontal edges are start/end, so they swap sides in right-to-left text.</summary>
public sealed class AnimatedPositionedDirectional(Widget child, TimeSpan duration, float? start = null, float? top = null, float? end = null,
    float? bottom = null, float? width = null, float? height = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        return new AnimatedPositioned(child, duration, rtl ? end : start, top, rtl ? start : end, bottom, width, height, curve, onEnd);
    }
}

/// <summary>A <see cref="Positioned"/> that animates its edges; use inside a <see cref="Stack"/>.</summary>
public sealed class AnimatedPositioned(Widget child, TimeSpan duration, float? left = null, float? top = null, float? right = null,
    float? bottom = null, float? width = null, float? height = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget Child => child;
    internal float? Left => left;
    internal float? Top => top;
    internal float? Right => right;
    internal float? Bottom => bottom;
    internal float? Width => width;
    internal float? Height => height;
    public override State CreateState() => new AnimatedPositionedState();
}

sealed class AnimatedPositionedState : ImplicitlyAnimatedWidgetState<AnimatedPositioned>
{
    Tween<float>? _left, _top, _right, _bottom, _width, _height;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _left = v.VisitValue(_left, Widget.Left, a => new FloatTween(a, a));
        _top = v.VisitValue(_top, Widget.Top, a => new FloatTween(a, a));
        _right = v.VisitValue(_right, Widget.Right, a => new FloatTween(a, a));
        _bottom = v.VisitValue(_bottom, Widget.Bottom, a => new FloatTween(a, a));
        _width = v.VisitValue(_width, Widget.Width, a => new FloatTween(a, a));
        _height = v.VisitValue(_height, Widget.Height, a => new FloatTween(a, a));
    }

    float? Eval(Tween<float>? t) => t?.Evaluate(Animation);

    public override Widget Build(BuildContext context) =>
        new Positioned(Widget.Child, Eval(_left), Eval(_top), Eval(_right), Eval(_bottom), Eval(_width), Eval(_height));
}

public sealed class AnimatedDefaultTextStyle(TextStyle style, TimeSpan duration, Widget child, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal TextStyle Style => style;
    internal Widget Child => child;
    public override State CreateState() => new AnimatedDefaultTextStyleState();
}

sealed class AnimatedDefaultTextStyleState : ImplicitlyAnimatedWidgetState<AnimatedDefaultTextStyle>
{
    Tween<TextStyle>? _style;
    protected override void ForEachTween(ITweenVisitor v) => _style = v.Visit(_style, Widget.Style, a => new TextStyleTween(a, a));
    public override Widget Build(BuildContext context) => DefaultTextStyle.Merge(context, _style!.Evaluate(Animation), Widget.Child);
}

public sealed class AnimatedScale(float scale, TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null,
    Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => scale;
    internal Widget? Child => child;
    internal Alignment? Origin => alignment;
    public override State CreateState() => new AnimatedScaleState();
}

sealed class AnimatedScaleState : ImplicitlyAnimatedWidgetState<AnimatedScale>
{
    Tween<float>? _scale;
    protected override void ForEachTween(ITweenVisitor v) => _scale = v.VisitValue(_scale, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => Transform.Scale(_scale!.Evaluate(Animation), Widget.Child, Widget.Origin);
}

/// <summary>Animates rotation in turns (1.0 = a full revolution).</summary>
public sealed class AnimatedRotation(float turns, TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null,
    Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal float Target => turns;
    internal Widget? Child => child;
    internal Alignment? Origin => alignment;
    public override State CreateState() => new AnimatedRotationState();
}

sealed class AnimatedRotationState : ImplicitlyAnimatedWidgetState<AnimatedRotation>
{
    Tween<float>? _turns;
    protected override void ForEachTween(ITweenVisitor v) => _turns = v.VisitValue(_turns, Widget.Target, a => new FloatTween(a, a));
    public override Widget Build(BuildContext context) => Transform.Rotate(_turns!.Evaluate(Animation) * MathF.PI * 2, Widget.Child, Widget.Origin);
}

/// <summary>Animates a translation expressed as a fraction of the child's own size.</summary>
public sealed class AnimatedSlide(Offset offset, TimeSpan duration, Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Offset Target => offset;
    internal Widget? Child => child;
    public override State CreateState() => new AnimatedSlideState();
}

sealed class AnimatedSlideState : ImplicitlyAnimatedWidgetState<AnimatedSlide>
{
    Tween<Offset>? _offset;
    protected override void ForEachTween(ITweenVisitor v) => _offset = v.VisitValue(_offset, Widget.Target, a => new OffsetTween(a, a));
    public override Widget Build(BuildContext context) => new FractionalTranslation(_offset!.Evaluate(Animation), Widget.Child);
}

/// <summary>Animates from the tween's begin to its end on first build, and to each new end when it changes.</summary>
public sealed class TweenAnimationBuilder<T>(Tween<T> tween, TimeSpan duration, Func<BuildContext, T, Widget?, Widget> builder,
    Widget? child = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key) where T : notnull
{
    internal Tween<T> Tween => tween;
    internal Func<BuildContext, T, Widget?, Widget> Builder => builder;
    internal Widget? Child => child;
    public override State CreateState() => new TweenAnimationBuilderState<T>();
}

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

// ---- switching and resizing ----

/// <summary>Cross-fades (or runs a custom transition) between its old and new child when the child's type or key changes.</summary>
public sealed class AnimatedSwitcher(TimeSpan duration, Widget? child = null, Func<Widget, Animation<float>, Widget>? transitionBuilder = null,
    TimeSpan? reverseDuration = null, Curve? switchInCurve = null, Curve? switchOutCurve = null, IAlignment? alignment = null, Key? key = null) : StatefulWidget(key)
{
    internal TimeSpan Duration => duration;
    internal TimeSpan ReverseDuration => reverseDuration ?? duration;
    internal Widget? Child => child;
    internal Func<Widget, Animation<float>, Widget> Transition => transitionBuilder ?? ((c, a) => new FadeTransition(a, c));
    internal Curve InCurve => switchInCurve ?? Curves.Linear;
    internal Curve OutCurve => switchOutCurve ?? Curves.Linear;
    internal IAlignment Alignment => alignment ?? Sway.Widgets.Alignment.Center;
    public override State CreateState() => new AnimatedSwitcherState();
}

sealed class AnimatedSwitcherState : TickerProviderState<AnimatedSwitcher>
{
    sealed class Entry(Widget? child, AnimationController controller, CurvedAnimation animation)
    {
        public Widget? Child = child;
        public readonly AnimationController Controller = controller;
        public readonly CurvedAnimation Animation = animation;
    }

    readonly List<Entry> _entries = new();
    Entry? _current;

    public override void InitState() => _current = Add(Widget.Child, initial: true);

    Entry Add(Widget? child, bool initial)
    {
        var controller = new AnimationController(this, Widget.Duration, Widget.ReverseDuration);
        var animation = new CurvedAnimation(controller, Widget.InCurve, Widget.OutCurve.Flipped);
        var entry = new Entry(child, controller, animation);
        controller.AddListener(() => { if (Mounted) SetState(); });
        if (initial) controller.SetValue(1); else controller.Forward();
        _entries.Add(entry);
        return entry;
    }

    void Remove(Entry e)
    {
        e.Animation.Dispose();
        e.Controller.Dispose();
        _entries.Remove(e);
    }

    public override void DidUpdateWidget(AnimatedSwitcher old)
    {
        bool same = _current is not null && Widget.Child is not null && _current.Child is not null && Sway.Widgets.Widget.CanUpdate(_current.Child, Widget.Child)
            || (_current?.Child is null && Widget.Child is null);
        if (same)
        {
            _current!.Child = Widget.Child;
            return;
        }

        if (_current is { } outgoing)
        {
            outgoing.Controller.AddStatusListener(s => { if (s == AnimationStatus.Dismissed && Mounted) SetState(() => Remove(outgoing)); });
            outgoing.Controller.Reverse();
        }
        _current = Widget.Child is null ? null : Add(Widget.Child, initial: false);
    }

    public override void Dispose()
    {
        foreach (var e in _entries.ToArray()) Remove(e);
        base.Dispose();
    }

    public override Widget Build(BuildContext context)
    {
        var children = new List<Widget>();
        foreach (var e in _entries)
            if (e.Child is { } child) children.Add(new KeyedSubtree(new ObjectKey(e), Widget.Transition(child, e.Animation)));
        return new Stack(children, alignment: Widget.Alignment, clip: false);
    }
}

public sealed class AnimatedSize(TimeSpan duration, Widget? child = null, Alignment? alignment = null, Curve? curve = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderAnimatedSize(duration, curve ?? Curves.Linear, alignment ?? Alignment.TopCenter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderAnimatedSize)ro).Update(duration, curve ?? Curves.Linear, alignment ?? Alignment.TopCenter);
}

/// <summary>Sizes itself to its child, easing from the previous size whenever the child's size changes.</summary>
public sealed class RenderAnimatedSize(TimeSpan duration, Curve curve, Alignment alignment) : RenderObjectWithChildBox
{
    TimeSpan _duration = duration;
    Curve _curve = curve;
    Alignment _alignment = alignment;
    Size? _from, _target;
    TimeSpan _start;
    bool _animating;

    public void Update(TimeSpan duration, Curve curve, Alignment alignment)
    {
        _duration = duration; _curve = curve;
        if (_alignment != alignment) { _alignment = alignment; MarkNeedsPaint(); }
    }

    protected override void PerformLayout()
    {
        if (Child is not { } child) { Size = Constraints.Smallest; return; }
        child.Layout(Constraints);
        var childSize = child.Size;
        var now = WidgetsBinding.Instance.Now;

        Size current;
        if (_target is null) { _target = childSize; current = childSize; }
        else
        {
            if (_target != childSize)
            {
                _from = CurrentSize(now);
                _target = childSize;
                _start = now;
                _animating = _duration > TimeSpan.Zero;
            }
            current = CurrentSize(now);
            if (_animating && now - _start >= _duration) _animating = false;
        }

        if (_animating)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => MarkNeedsLayout());
        Size = Constraints.Constrain(current);
        SetOffset(child, _alignment.AlongSize(Size, childSize));
    }

    Size CurrentSize(TimeSpan now)
    {
        if (!_animating || _from is null) return _target!.Value;
        float t = Math.Clamp((float)((now - _start) / _duration), 0, 1);
        return Lerps.Size(_from.Value, _target!.Value, _curve.Transform(t));
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is { } c)
            context.PushClipRect(offset, Size.ToRect(), (ctx, o) => ctx.PaintChild(c, o + OffsetOf(c)));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && HitTestChild(c, result, position);
}

public enum CrossFadeState { ShowFirst, ShowSecond }

public sealed class AnimatedCrossFade(Widget firstChild, Widget secondChild, CrossFadeState crossFadeState, TimeSpan duration,
    Curve? curve = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget First => firstChild;
    internal Widget Second => secondChild;
    internal CrossFadeState State => crossFadeState;
    internal TimeSpan Duration => duration;
    internal Curve Curve => curve ?? Curves.Linear;
    public override State CreateState() => new AnimatedCrossFadeState();
}

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

sealed class Reversed(Animation<float> parent) : Animation<float>
{
    bool _hooked;
    public override float Value => 1 - parent.Value;
    public override AnimationStatus Status => parent.Status;
    public override void AddListener(Action l)
    {
        if (!_hooked) { _hooked = true; parent.AddListener(NotifyListeners); }
        base.AddListener(l);
    }
}

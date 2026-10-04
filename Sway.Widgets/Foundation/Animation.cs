using SkiaSharp;

namespace Sway.Widgets;

public enum AnimationStatus { Dismissed, Forward, Reverse, Completed }

public interface IListenable
{
    void AddListener(Action listener);
    void RemoveListener(Action listener);
}

/// <summary>Calls back once per frame while running, with the time since <see cref="Start"/>.</summary>
public sealed class Ticker(Action<TimeSpan> onTick)
{
    TimeSpan _start;
    int _generation;

    public bool IsActive { get; private set; }

    public void Start()
    {
        _generation++;
        IsActive = true;
        _start = WidgetsBinding.Instance.Now;
        Schedule();
    }

    public void Stop()
    {
        _generation++;
        IsActive = false;
    }

    void Schedule()
    {
        int gen = _generation;
        WidgetsBinding.Instance.ScheduleFrameCallback(now =>
        {
            if (!IsActive || gen != _generation) return;
            onTick(now - _start);
            if (IsActive && gen == _generation) Schedule();
        });
    }
}

public interface ITickerProvider
{
    Ticker CreateTicker(Action<TimeSpan> onTick);
}

/// <summary>Base for states that own animation controllers; tickers stop when the state is disposed.</summary>
public abstract class TickerProviderState<T> : State<T>, ITickerProvider where T : StatefulWidget
{
    readonly List<Ticker> _tickers = new();

    public Ticker CreateTicker(Action<TimeSpan> onTick)
    {
        var t = new Ticker(onTick);
        _tickers.Add(t);
        return t;
    }

    public override void Dispose()
    {
        foreach (var t in _tickers) t.Stop();
        _tickers.Clear();
    }
}

/// <summary>A value that changes over time and notifies listeners.</summary>
public abstract class Animation<T> : IListenable
{
    event Action? _changed;
    event Action<AnimationStatus>? _statusChanged;

    public abstract T Value { get; }
    public abstract AnimationStatus Status { get; }

    public virtual void AddListener(Action listener) => _changed += listener;
    public void RemoveListener(Action listener) => _changed -= listener;
    public void AddStatusListener(Action<AnimationStatus> listener) => _statusChanged += listener;
    public void RemoveStatusListener(Action<AnimationStatus> listener) => _statusChanged -= listener;

    protected void NotifyListeners() => _changed?.Invoke();
    protected void NotifyStatus(AnimationStatus s) => _statusChanged?.Invoke(s);

    public bool IsCompleted => Status == AnimationStatus.Completed;
    public bool IsDismissed => Status == AnimationStatus.Dismissed;
    public bool IsAnimating => Status is AnimationStatus.Forward or AnimationStatus.Reverse;

    public Animation<U> Drive<U>(Animatable<U> child) where U : notnull => child.Animate(AsFloat());

    Animation<float> AsFloat() => this as Animation<float> ?? throw new InvalidOperationException("Only Animation<float> can drive a tween.");
}

public sealed class AlwaysStoppedAnimation<T>(T value) : Animation<T>
{
    public override T Value => value;
    public override AnimationStatus Status => AnimationStatus.Forward;
}

/// <summary>Drives a value from <see cref="LowerBound"/> to <see cref="UpperBound"/> over a duration, one frame at a time.</summary>
public sealed class AnimationController : Animation<float>, IDisposable
{
    readonly Ticker _ticker;
    float _value;
    AnimationStatus _status = AnimationStatus.Dismissed;

    // current run
    float _from, _to;
    TimeSpan _runDuration;
    Curve _runCurve = Curves.Linear;
    bool _repeating, _repeatReverse;
    float _repeatMin, _repeatMax;
    TimeSpan _repeatPeriod;
    bool _disposed;

    public AnimationController(ITickerProvider vsync, TimeSpan? duration = null, TimeSpan? reverseDuration = null,
        float lowerBound = 0, float upperBound = 1, float? value = null)
    {
        Duration = duration;
        ReverseDuration = reverseDuration;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        _value = value ?? lowerBound;
        _status = _value >= upperBound ? AnimationStatus.Completed : AnimationStatus.Dismissed;
        _ticker = vsync.CreateTicker(OnTick);
    }

    public TimeSpan? Duration { get; set; }
    public TimeSpan? ReverseDuration { get; set; }
    public float LowerBound { get; }
    public float UpperBound { get; }

    public override float Value => _value;
    public override AnimationStatus Status => _status;
    public bool IsRunning => _ticker.IsActive;

    /// <summary>Jumps to a value, stopping any running animation.</summary>
    public void SetValue(float v)
    {
        Stop();
        Set(Math.Clamp(v, LowerBound, UpperBound));
        SetStatus(_value >= UpperBound ? AnimationStatus.Completed : _value <= LowerBound ? AnimationStatus.Dismissed : _status);
    }

    float Range => Math.Max(1e-6f, UpperBound - LowerBound);

    public void Forward(float? from = null)
    {
        if (from is { } f) Set(Math.Clamp(f, LowerBound, UpperBound));
        Run(UpperBound, Duration, Curves.Linear, AnimationStatus.Forward);
    }

    public void Reverse(float? from = null)
    {
        if (from is { } f) Set(Math.Clamp(f, LowerBound, UpperBound));
        Run(LowerBound, ReverseDuration ?? Duration, Curves.Linear, AnimationStatus.Reverse);
    }

    public void AnimateTo(float target, TimeSpan? duration = null, Curve? curve = null) =>
        Run(Math.Clamp(target, LowerBound, UpperBound), duration ?? Duration, curve ?? Curves.Linear,
            target >= _value ? AnimationStatus.Forward : AnimationStatus.Reverse);

    public void Toggle() { if (_status is AnimationStatus.Forward or AnimationStatus.Completed) Reverse(); else Forward(); }

    public void Reset() => SetValue(LowerBound);

    /// <summary>Loops between two values; with <paramref name="reverse"/> it bounces back and forth.</summary>
    public void Repeat(float? min = null, float? max = null, bool reverse = false, TimeSpan? period = null)
    {
        _repeating = true;
        _repeatReverse = reverse;
        _repeatMin = min ?? LowerBound;
        _repeatMax = max ?? UpperBound;
        _repeatPeriod = period ?? Duration ?? throw new InvalidOperationException("Repeat needs a duration.");
        float start = Math.Clamp(_value, _repeatMin, _repeatMax);
        if (start >= _repeatMax && !reverse) start = _repeatMin;
        Set(start);
        StartRun(_repeatMax, ScaledDuration(_repeatPeriod, start, _repeatMax, _repeatMax - _repeatMin), Curves.Linear, AnimationStatus.Forward);
    }

    public void Stop()
    {
        _repeating = false;
        _ticker.Stop();
    }

    void Run(float target, TimeSpan? duration, Curve curve, AnimationStatus direction)
    {
        _repeating = false;
        if (duration is null) throw new InvalidOperationException("AnimationController needs a duration.");
        var d = ScaledDuration(duration.Value, _value, target, Range);
        if (d <= TimeSpan.Zero || _value == target)
        {
            _ticker.Stop();
            Set(target);
            SetStatus(target >= UpperBound ? AnimationStatus.Completed : AnimationStatus.Dismissed);
            return;
        }
        StartRun(target, d, curve, direction);
    }

    // A run that covers part of the range takes the matching part of the duration.
    static TimeSpan ScaledDuration(TimeSpan full, float from, float to, float range) =>
        TimeSpan.FromTicks((long)(full.Ticks * Math.Min(1, MathF.Abs(to - from) / Math.Max(1e-6f, range))));

    void StartRun(float target, TimeSpan duration, Curve curve, AnimationStatus direction)
    {
        _from = _value;
        _to = target;
        _runDuration = duration;
        _runCurve = curve;
        SetStatus(direction);
        _ticker.Start();
    }

    void OnTick(TimeSpan elapsed)
    {
        if (_disposed) return;
        float t = _runDuration <= TimeSpan.Zero ? 1 : Math.Clamp((float)(elapsed / _runDuration), 0, 1);
        Set(_from + (_to - _from) * _runCurve.Transform(t));
        if (t < 1) return;

        _value = _to;
        if (_repeating)
        {
            if (_repeatReverse)
            {
                bool wasForward = _status == AnimationStatus.Forward;
                float next = wasForward ? _repeatMin : _repeatMax;
                StartRun(next, _repeatPeriod, Curves.Linear, wasForward ? AnimationStatus.Reverse : AnimationStatus.Forward);
            }
            else
            {
                Set(_repeatMin);
                StartRun(_repeatMax, _repeatPeriod, Curves.Linear, AnimationStatus.Forward);
            }
            return;
        }
        _ticker.Stop();
        SetStatus(_to >= UpperBound ? AnimationStatus.Completed : AnimationStatus.Dismissed);
    }

    void Set(float v)
    {
        if (v == _value) return;
        _value = v;
        NotifyListeners();
    }

    void SetStatus(AnimationStatus s)
    {
        if (s == _status) return;
        _status = s;
        NotifyStatus(s);
    }

    public void Dispose()
    {
        _disposed = true;
        _ticker.Stop();
    }
}

/// <summary>Applies a <see cref="Curve"/> to another animation (with an optional different curve when reversing).</summary>
public sealed class CurvedAnimation : Animation<float>
{
    readonly Animation<float> _parent;
    readonly Action _onParent;
    readonly Action<AnimationStatus> _onStatus;

    public CurvedAnimation(Animation<float> parent, Curve curve, Curve? reverseCurve = null)
    {
        _parent = parent;
        Curve = curve;
        ReverseCurve = reverseCurve;
        _onParent = NotifyListeners;
        _onStatus = NotifyStatus;
        parent.AddListener(_onParent);
        parent.AddStatusListener(_onStatus);
    }

    public Curve Curve { get; set; }
    public Curve? ReverseCurve { get; set; }
    public override AnimationStatus Status => _parent.Status;

    public override float Value
    {
        get
        {
            bool reversing = ReverseCurve is not null && _parent.Status is AnimationStatus.Reverse or AnimationStatus.Dismissed;
            return (reversing ? ReverseCurve! : Curve).Transform(_parent.Value);
        }
    }

    public void Dispose()
    {
        _parent.RemoveListener(_onParent);
        _parent.RemoveStatusListener(_onStatus);
    }
}

/// <summary>Something that can be evaluated at progress t (0..1): a <see cref="Tween{T}"/> or a <see cref="TweenSequence{T}"/>.</summary>
public abstract class Animatable<T> where T : notnull
{
    public abstract T Transform(float t);
    public T Evaluate(Animation<float> animation) => Transform(animation.Value);

    public Animation<T> Animate(Animation<float> parent) => new AnimatedEvaluation<T>(parent, this);

    /// <summary>Applies <paramref name="curve"/> to the progress before this animatable sees it.</summary>
    public Animatable<T> Curved(Curve curve) => new CurvedAnimatable<T>(this, curve);
}

sealed class CurvedAnimatable<T>(Animatable<T> inner, Curve curve) : Animatable<T> where T : notnull
{
    public override T Transform(float t) => inner.Transform(curve.Transform(t));
}

sealed class AnimatedEvaluation<T>(Animation<float> parent, Animatable<T> animatable) : Animation<T> where T : notnull
{
    bool _hooked;

    public override T Value => animatable.Transform(parent.Value);
    public override AnimationStatus Status => parent.Status;

    // Subscribe to the parent lazily so unused evaluations do not leak listeners.
    public override void AddListener(Action l) { Hook(); base.AddListener(l); }
    void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        parent.AddListener(NotifyListeners);
        parent.AddStatusListener(NotifyStatus);
    }
}

/// <summary>Interpolates between <see cref="Begin"/> and <see cref="End"/> with a type-specific blend function.</summary>
public class Tween<T>(T begin, T end, Func<T, T, float, T> lerp) : Animatable<T> where T : notnull
{
    public T Begin { get; set; } = begin;
    public T End { get; set; } = end;

    public override T Transform(float t) => t == 0 ? Begin : t == 1 ? End : lerp(Begin, End, t);
}

public sealed class FloatTween(float begin, float end) : Tween<float>(begin, end, Lerps.Float);
public sealed class ColorTween(SKColor begin, SKColor end) : Tween<SKColor>(begin, end, Lerps.Color);
public sealed class OffsetTween(Offset begin, Offset end) : Tween<Offset>(begin, end, Lerps.Offset);
public sealed class SizeTween(Size begin, Size end) : Tween<Size>(begin, end, Lerps.Size);
public sealed class EdgeInsetsTween(EdgeInsets begin, EdgeInsets end) : Tween<EdgeInsets>(begin, end, Lerps.EdgeInsets);
public sealed class AlignmentTween(Alignment begin, Alignment end) : Tween<Alignment>(begin, end, Lerps.Alignment);
public sealed class BoxConstraintsTween(BoxConstraints begin, BoxConstraints end) : Tween<BoxConstraints>(begin, end, Lerps.BoxConstraints);
public sealed class BorderRadiusTween(BorderRadius begin, BorderRadius end) : Tween<BorderRadius>(begin, end, Lerps.BorderRadius);
public sealed class TextStyleTween(TextStyle begin, TextStyle end) : Tween<TextStyle>(begin, end, Lerps.TextStyle);
public sealed class MatrixTween(SKMatrix begin, SKMatrix end) : Tween<SKMatrix>(begin, end, Lerps.Matrix);
public sealed class DecorationTween(BoxDecoration begin, BoxDecoration end) : Tween<BoxDecoration>(begin, end, (a, b, t) => Lerps.BoxDecoration(a, b, t));

/// <summary>Plays several tweens one after another, each taking a share of the time proportional to its weight (keyframes).</summary>
public sealed class TweenSequence<T> : Animatable<T> where T : notnull
{
    readonly List<(Animatable<T> item, float start, float end)> _items = new();

    public TweenSequence(IEnumerable<(Animatable<T> item, float weight)> items)
    {
        var list = items.ToList();
        float total = list.Sum(i => i.weight), acc = 0;
        foreach (var (item, weight) in list)
        {
            _items.Add((item, acc / total, (acc + weight) / total));
            acc += weight;
        }
    }

    public override T Transform(float t)
    {
        foreach (var (item, start, end) in _items)
            if (t <= end) return item.Transform(end == start ? 1 : (t - start) / (end - start));
        return _items[^1].item.Transform(1);
    }
}

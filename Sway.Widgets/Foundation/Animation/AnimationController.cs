using SkiaSharp;

namespace Sway.Widgets;

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
    Simulation? _simulation;
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

    /// <summary>
    /// Flings toward the upper bound (or the lower one if <paramref name="velocity"/> is negative) on a spring.
    /// The velocity is in fractions of the bound-to-bound range per second.
    /// </summary>
    public void Fling(float velocity = 1, SpringDescription? spring = null)
    {
        var description = spring ?? SpringDescription.WithDampingRatio(1, 500);
        float target = velocity < 0 ? LowerBound : UpperBound;
        // The spring settles into its target within a thousandth of the range.
        var simulation = new SpringSimulation(description, _value, target, velocity * Range)
            { Tolerance = new Tolerance(Range * 1e-3f, Range * 1e-3f) };
        AnimateWith(simulation, velocity < 0 ? AnimationStatus.Reverse : AnimationStatus.Forward);
    }

    /// <summary>Drives the value with a physics simulation; the value is kept inside the bounds.</summary>
    public void AnimateWith(Simulation simulation) =>
        AnimateWith(simulation, simulation.Dx(0) < 0 ? AnimationStatus.Reverse : AnimationStatus.Forward);

    void AnimateWith(Simulation simulation, AnimationStatus direction)
    {
        _repeating = false;
        _simulation = simulation;
        SetStatus(direction);
        _ticker.Start();
    }

    public void Stop()
    {
        _repeating = false;
        _simulation = null;
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
        _simulation = null;
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
        if (_simulation is { } sim)
        {
            float seconds = (float)elapsed.TotalSeconds;
            float v = Math.Clamp(sim.X(seconds), LowerBound, UpperBound);
            bool finished = sim.IsDone(seconds) || (v == LowerBound && sim.Dx(seconds) < 0) || (v == UpperBound && sim.Dx(seconds) > 0);
            Set(v);
            if (!finished) return;

            _simulation = null;
            _ticker.Stop();
            SetStatus(_status == AnimationStatus.Forward ? AnimationStatus.Completed : AnimationStatus.Dismissed);
            return;
        }

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

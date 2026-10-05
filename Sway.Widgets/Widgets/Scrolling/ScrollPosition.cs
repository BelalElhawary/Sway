using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Scroll offset and extents for one scrollable, plus fling and animated scrolling.</summary>
public sealed class ScrollPosition
{
    static readonly TimeSpan WheelDuration = TimeSpan.FromMilliseconds(180);
    static readonly float FlingDrag = MathF.Exp(-3.2f);

    int _activity; // bumps on every new activity so stale frame callbacks stop
    float _wheelTarget;
    int _wheelActivity = -1;

    public float Pixels { get; private set; }
    public float MaxScrollExtent { get; private set; }
    public float ViewportExtent { get; private set; }
    public bool CanScroll => MaxScrollExtent > 0;
    public TimeSpan LastChange { get; private set; } = TimeSpan.FromHours(-1);

    /// <summary>True while the scrollbar owns the pointer, so the drag recognizer underneath ignores it.</summary>
    public bool ScrollbarActive { get; set; }

    public event Action? Changed;

    public void ApplyContentDimensions(float viewportExtent, float maxScrollExtent)
    {
        ViewportExtent = viewportExtent;
        MaxScrollExtent = Math.Max(0, maxScrollExtent);
        if (Pixels > MaxScrollExtent) Pixels = MaxScrollExtent;
    }

    /// <summary>Moves to <paramref name="value"/> (clamped). Returns how far it actually moved.</summary>
    public float JumpTo(float value)
    {
        value = Math.Clamp(value, 0, MaxScrollExtent);
        float moved = value - Pixels;
        if (moved == 0) return 0;
        Pixels = value;
        LastChange = WidgetsBinding.Instance.Now;
        Changed?.Invoke();
        return moved;
    }

    public void StopActivity() => _activity++;

    /// <summary>Adjusts the offset during layout (variable-height lists re-anchor as items are measured) without notifying.</summary>
    internal void CorrectTo(float value) => Pixels = Math.Clamp(value, 0, MaxScrollExtent);

    /// <summary>
    /// Scrolls by a wheel notch with a short eased animation. Notches arriving mid-animation accumulate onto the
    /// pending target. Returns false if the scrollable is already at that edge, so an outer one can take over.
    /// </summary>
    public bool ScrollBy(float delta)
    {
        float basis = _wheelActivity == _activity ? _wheelTarget : Pixels;
        float target = Math.Clamp(basis + delta, 0, MaxScrollExtent);
        if (target == basis) return false;
        AnimateTo(target, WheelDuration, Curves.EaseOut);
        _wheelTarget = target;
        _wheelActivity = _activity;
        return true;
    }

    /// <summary>Continues scrolling at <paramref name="velocity"/> px/s, slowing with friction.</summary>
    public void Fling(float velocity)
    {
        StopActivity();
        if (Math.Abs(velocity) < 50) return;
        int id = _activity;
        // Velocity halves roughly every 0.2s; the scroll stops once it falls below 25 px/s.
        var simulation = new FrictionSimulation(FlingDrag, Pixels, Math.Clamp(velocity, -8000, 8000))
            { Tolerance = new Tolerance(1, 25) };
        TimeSpan? start = null;

        void Step(TimeSpan now)
        {
            if (id != _activity) return;
            start ??= now;
            float seconds = (float)(now - start.Value).TotalSeconds;
            float wanted = simulation.X(seconds);
            JumpTo(wanted);
            bool hitEdge = wanted < 0 || wanted > MaxScrollExtent;
            if (simulation.IsDone(seconds) || hitEdge) return;
            WidgetsBinding.Instance.ScheduleFrameCallback(Step);
        }
        WidgetsBinding.Instance.ScheduleFrameCallback(Step);
    }

    public void AnimateTo(float target, TimeSpan duration, Curve? curve = null)
    {
        StopActivity();
        int id = _activity;
        float from = Pixels;
        target = Math.Clamp(target, 0, MaxScrollExtent);
        curve ??= Curves.EaseOut;
        TimeSpan? start = null;

        void Step(TimeSpan now)
        {
            if (id != _activity) return;
            start ??= now;
            float t = duration <= TimeSpan.Zero ? 1 : Math.Clamp((float)((now - start.Value) / duration), 0, 1);
            JumpTo(from + (target - from) * curve.Transform(t));
            if (t < 1) WidgetsBinding.Instance.ScheduleFrameCallback(Step);
        }
        WidgetsBinding.Instance.ScheduleFrameCallback(Step);
    }
}

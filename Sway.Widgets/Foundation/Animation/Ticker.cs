using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Calls back once per frame while running, with the time since <see cref="Start"/>.</summary>
public sealed class Ticker(Action<TimeSpan> onTick)
{
    TimeSpan _start;
    bool _scheduled, _muted;

    public bool IsActive { get; private set; }

    /// <summary>
    /// While muted the ticker keeps its place in time but stops calling back and stops asking for frames, so a hidden animation
    /// does not keep the window awake. Unmuting resumes it where the clock now is.
    /// </summary>
    public bool Muted
    {
        get => _muted;
        set
        {
            if (_muted == value) return;
            _muted = value;
            if (!value && IsActive) Schedule();
        }
    }

    public void Start()
    {
        IsActive = true;
        _start = WidgetsBinding.Instance.Now;
        Schedule();
    }

    public void Stop()
    {
        IsActive = false;
    }

    void Schedule()
    {
        if (_scheduled || _muted) return;
        _scheduled = true;
        // At most one callback is ever pending, so a stop/start before it fires simply reuses it.
        WidgetsBinding.Instance.ScheduleFrameCallback(now =>
        {
            _scheduled = false;
            if (!IsActive || _muted) return;
            onTick(now - _start);
            if (IsActive) Schedule();
        });
    }
}

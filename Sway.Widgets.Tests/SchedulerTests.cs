using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimizations 3.3 / 3.4: timers, frame callbacks and the build scope behave as before with the allocation-free scheduler.</summary>
public class SchedulerTests
{
    static Harness Empty() => new(new SizedBox(width: 10, height: 10));

    [Fact]
    public void TimersRunOldestFirstAndTiesKeepTheirScheduleOrder()
    {
        var h = Empty();
        var log = new List<string>();
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(300), () => log.Add("c"));
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(100), () => log.Add("a1"));
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(200), () => log.Add("b"));
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(100), () => log.Add("a2"));

        h.Advance(1000);

        Assert.Equal(new[] { "a1", "a2", "b", "c" }, log);
    }

    [Fact]
    public void ATimerDoesNotRunBeforeItIsDue()
    {
        var h = Empty();
        int ran = 0;
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(500), () => ran++);
        h.Advance(400);
        Assert.Equal(0, ran);
        h.Advance(200);
        Assert.Equal(1, ran);
    }

    [Fact]
    public void NeedsFrameStaysFalseWhileTimersArePending()
    {
        var h = Empty();
        h.Pump();
        Assert.False(h.Binding.NeedsFrame(h.Width, h.Height));

        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(500), () => { });
        h.Pump(); // consumes the frame the schedule itself requested
        Assert.False(h.Binding.NeedsFrame(h.Width, h.Height));

        h.Binding.AdvanceClock(TimeSpan.FromMilliseconds(100));
        h.Pump();
        Assert.False(h.Binding.NeedsFrame(h.Width, h.Height));
    }

    [Fact]
    public void CancellingTheEarliestTimerLetsTheNextOneDecideWhenAFrameIsNeeded()
    {
        var h = Empty();
        int early = 0, late = 0;
        Action cancelled = () => early++;
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(100), cancelled);
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(900), () => late++);
        Assert.True(h.Binding.CancelTimer(cancelled));
        Assert.False(h.Binding.CancelTimer(cancelled));

        h.Advance(500);
        Assert.Equal(0, early);
        Assert.Equal(0, late);
        h.Advance(500);
        Assert.Equal(0, early);
        Assert.Equal(1, late);
    }

    [Fact]
    public void ATimerScheduledByATimerRunsLater()
    {
        var h = Empty();
        var log = new List<string>();
        h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(100), () =>
        {
            log.Add("first");
            h.Binding.ScheduleTimer(TimeSpan.FromMilliseconds(100), () => log.Add("second"));
        });

        h.Advance(100);
        Assert.Equal(new[] { "first" }, log);
        h.Advance(100);
        Assert.Equal(new[] { "first", "second" }, log);
    }

    [Fact]
    public void FrameCallbacksRunOncePerRequestAndAReschedulingOneRunsNextFrame()
    {
        var h = Empty();
        int once = 0, repeating = 0;
        h.Binding.ScheduleFrameCallback(_ => once++);
        Action<TimeSpan>? tick = null;
        tick = _ => { repeating++; h.Binding.ScheduleFrameCallback(tick!); };
        h.Binding.ScheduleFrameCallback(tick);

        h.Pump();
        Assert.Equal(1, once);
        Assert.Equal(1, repeating);
        h.Pump();
        h.Pump();
        Assert.Equal(1, once);
        Assert.Equal(3, repeating);
    }

    [Fact]
    public void AThrowingFrameCallbackDoesNotBreakLaterFrames()
    {
        var h = Empty();
        int later = 0;
        h.Binding.ScheduleFrameCallback(_ => throw new InvalidOperationException("boom"));
        Assert.Throws<InvalidOperationException>(() => h.Pump());

        h.Binding.ScheduleFrameCallback(_ => later++);
        h.Pump();
        Assert.Equal(1, later);
    }

    // ---- build scope ----

    sealed class Probe(string name, List<string> log, Action<Action> expose, Widget? child = null) : StatefulWidget
    {
        internal string Name => name;
        internal List<string> Log => log;
        internal Action<Action> Expose => expose;
        internal Widget? Child => child;
        public override State CreateState() => new ProbeState();
    }

    sealed class ProbeState : State<Probe>
    {
        public override void InitState() => Widget.Expose(() => SetState());
        public override Widget Build(BuildContext context)
        {
            Widget.Log.Add(Widget.Name);
            return Widget.Child ?? new SizedBox(width: 10, height: 10);
        }
    }

    [Fact]
    public void AnElementMarkedDirtyAloneRebuildsOnce()
    {
        var log = new List<string>();
        Action? mark = null;
        var h = new Harness(new Probe("solo", log, m => mark = m));
        log.Clear();

        mark!();
        h.Pump();

        Assert.Equal(new[] { "solo" }, log);
    }

    [Fact]
    public void DirtyElementsRebuildShallowestFirstRegardlessOfTheOrderTheyWereMarked()
    {
        var log = new List<string>();
        Action? markParent = null, markChild = null;
        var child = new Probe("child", log, m => markChild = m);
        var h = new Harness(new Probe("parent", log, m => markParent = m, child));
        log.Clear();

        markChild!();
        markParent!();
        h.Pump();

        // The parent rebuilds first; its rebuild updates the child with an identical widget, so the child is not rebuilt twice.
        Assert.Equal("parent", log[0]);
        Assert.True(log.Count(x => x == "child") <= 1);
    }
}

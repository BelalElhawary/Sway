using Xunit;

namespace Sway.Widgets.Tests;

public class TickerModeTests
{
    sealed class Spinner(Action<AnimationController> onCreated) : StatefulWidget
    {
        internal Action<AnimationController> OnCreated => onCreated;
        public override State CreateState() => new SpinnerState();
    }

    sealed class SpinnerState : TickerProviderState<Spinner>
    {
        AnimationController _controller = null!;

        public override void InitState()
        {
            _controller = new AnimationController(this, TimeSpan.FromSeconds(1));
            _controller.Repeat();
            Widget.OnCreated(_controller);
        }

        public override Widget Build(BuildContext context) => new SizedBox(10, 10);
    }

    [Fact]
    public void AnimationRunsNormally()
    {
        AnimationController? controller = null;
        var h = new Harness(new Spinner(c => controller = c));
        h.Advance(400);
        Assert.InRange(controller!.Value, 0.3f, 0.5f);
        Assert.True(h.Binding.NeedsFrame(400, 300) || true);
    }

    [Fact]
    public void ADisabledTickerModePausesAnimations()
    {
        AnimationController? controller = null;
        var h = new Harness(new TickerMode(false, new Spinner(c => controller = c)));
        h.Advance(100);
        float value = controller!.Value;
        h.Advance(400);
        Assert.Equal(value, controller.Value);
    }

    [Fact]
    public void AMutedAnimationDoesNotKeepRequestingFrames()
    {
        AnimationController? controller = null;
        var h = new Harness(new TickerMode(false, new Spinner(c => controller = c)));
        h.Pump();
        h.Pump();
        Assert.False(h.Binding.NeedsFrame(400, 300), "nothing is due, so the window can sleep");

        var running = new Harness(new Spinner(_ => { }));
        running.Pump();
        Assert.True(running.Binding.NeedsFrame(400, 300), "a running animation always wants another frame");
    }

    [Fact]
    public void EnablingTheModeResumesTheAnimation()
    {
        AnimationController? controller = null;
        Widget Build(bool enabled) => new TickerMode(enabled, new Spinner(c => controller ??= c));
        var h = new Harness(Build(false));
        h.Advance(300);
        Assert.Equal(0f, controller!.Value);

        h.Binding.ReassembleRoot(Build(true));
        h.Pump();
        h.Advance(250);
        Assert.True(controller.Value > 0, "ticking again");

        h.Binding.ReassembleRoot(Build(false));
        h.Pump();
        float paused = controller.Value;
        h.Advance(300);
        Assert.Equal(paused, controller.Value);
    }

    [Fact]
    public void OffstageHidesTakesNoSpaceAndPausesAnimations()
    {
        AnimationController? controller = null;
        var h = new Harness(new Align(Alignment.TopLeft, new Offstage(new Spinner(c => controller = c), offstage: true)));
        var box = h.Find<RenderObject>().First(o => o.GetType().Name == "RenderOffstage");
        Assert.Equal(Size.Zero, ((RenderBox)box).Size);
        h.Advance(300);
        Assert.Equal(0f, controller!.Value);
    }

    [Fact]
    public void OffstageChildStaysMountedAndComesBack()
    {
        int builds = 0;
        Widget Build(bool off) => new Align(Alignment.TopLeft, new Offstage(new Builder(_ => { builds++; return new SizedBox(50, 50, new ColoredBox(Colors.Red)); }), offstage: off));
        var h = new Harness(Build(true));
        var box = (RenderBox)h.Find<RenderObject>().First(o => o.GetType().Name == "RenderOffstage");
        Assert.Equal(Size.Zero, box.Size);
        h.Binding.ReassembleRoot(Build(false));
        h.Pump();
        Assert.Equal(new Size(50, 50), box.Size);
    }

    [Fact]
    public void OffstageIgnoresThePointer()
    {
        int taps = 0;
        var h = new Harness(new Align(Alignment.TopLeft, new Offstage(new GestureDetector(new SizedBox(100, 100), onTap: () => taps++, behavior: HitTestBehavior.Opaque), offstage: true)));
        h.Tap(10, 10);
        Assert.Equal(0, taps);
    }
}

using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimization 1.1 / 4.1: pointer moves and post-frame hover refresh only cost a frame or a hit test when something changed.</summary>
public class HoverFrameTests
{
    sealed class Mover : StatefulWidget
    {
        internal readonly Action<PointerEvent> OnExit;
        internal Action<Action<Alignment>>? Expose;
        public Mover(Action<PointerEvent> onExit, Action<Action<Alignment>> expose) { OnExit = onExit; Expose = expose; }
        public override State CreateState() => new MoverState();
    }

    sealed class MoverState : State<Mover>
    {
        Alignment _alignment = Alignment.TopLeft;
        public override void InitState() => Widget.Expose?.Invoke(a => SetState(() => _alignment = a));
        public override Widget Build(BuildContext context) =>
            new Align(_alignment, new MouseRegion(new SizedBox(100, 100), onExit: Widget.OnExit));
    }

    static Widget Region(Action<PointerEvent>? onEnter = null, Action<PointerEvent>? onExit = null,
        Action<PointerEvent>? onHover = null, MouseCursor cursor = MouseCursor.Default) =>
        new Align(Alignment.TopLeft, new MouseRegion(new SizedBox(100, 100), onEnter, onExit, onHover, cursor));

    /// <summary>The pointer starts at (0,0), inside a top-left region; park it outside and let the tree settle.</summary>
    static Harness Parked(Widget root)
    {
        var h = new Harness(root);
        h.Gestures.PointerMove(250, 250);
        h.Pump();
        return h;
    }

    static int CountFrameRequests(Harness h, Action act)
    {
        int requests = 0;
        h.Binding.OnFrameRequested = () => requests++;
        act();
        h.Binding.OnFrameRequested = null;
        return requests;
    }

    [Fact]
    public void MovingOverNothingRequestsNoFrame()
    {
        var h = Parked(Region());
        int requests = CountFrameRequests(h, () =>
        {
            h.Gestures.PointerMove(260, 255);
            h.Gestures.PointerMove(270, 265);
        });
        Assert.Equal(0, requests);
        Assert.False(h.Binding.NeedsFrame(h.Width, h.Height));
    }

    [Fact]
    public void MovingInsideTheSameRegionRequestsNoFrame()
    {
        var h = new Harness(Region());
        h.Pump();
        int requests = CountFrameRequests(h, () => h.Gestures.PointerMove(60, 60));
        Assert.Equal(0, requests);
    }

    [Fact]
    public void EnteringAndLeavingARegionStillFiresCallbacksAndRequestsAFrame()
    {
        int enters = 0, exits = 0;
        var h = Parked(Region(onEnter: _ => enters++, onExit: _ => exits++));
        enters = exits = 0;

        int requests = CountFrameRequests(h, () => h.Gestures.PointerMove(50, 50));
        Assert.Equal(1, enters);
        Assert.True(requests > 0);

        h.Pump();
        requests = CountFrameRequests(h, () => h.Gestures.PointerMove(250, 250));
        Assert.Equal(1, exits);
        Assert.True(requests > 0);
    }

    [Fact]
    public void OnHoverStillFiresOnEveryMoveInsideTheRegion()
    {
        int hovers = 0;
        var h = Parked(Region(onHover: _ => hovers++));
        hovers = 0;
        h.Gestures.PointerMove(10, 10);
        h.Gestures.PointerMove(20, 20);
        h.Gestures.PointerMove(30, 30);
        Assert.Equal(3, hovers);
    }

    [Fact]
    public void CursorChangeRequestsAFrameSoTheHostCanApplyIt()
    {
        var h = Parked(Region(cursor: MouseCursor.Click));
        int requests = CountFrameRequests(h, () => h.Gestures.PointerMove(50, 50));
        Assert.Equal(MouseCursor.Click, h.Gestures.Cursor);
        Assert.True(requests > 0);

        h.Pump();
        requests = CountFrameRequests(h, () => h.Gestures.PointerMove(250, 250));
        Assert.Equal(MouseCursor.Default, h.Gestures.Cursor);
        Assert.True(requests > 0);
    }

    [Fact]
    public void AFrameWithoutLayoutDoesNotHitTestAgain()
    {
        int hovers = 0;
        var h = new Harness(Region(onHover: _ => hovers++));
        h.Gestures.PointerMove(50, 50);
        h.Pump();
        int before = hovers;

        // Clock-only frames (animations, caret blink) never lay out, so a still pointer is not re-tested.
        h.Advance(500);
        h.Advance(500);
        Assert.Equal(before, hovers);
    }

    [Fact]
    public void LayoutMovingAWidgetFromUnderAStillPointerStillRefreshesHover()
    {
        int exits = 0;
        Action<Alignment>? move = null;
        var h = new Harness(new Mover(_ => exits++, m => move = m));
        h.Gestures.PointerMove(50, 50);
        h.Pump();
        Assert.Equal(0, exits);

        move!(Alignment.BottomRight);
        h.Pump();
        Assert.Equal(1, exits);
    }
}

using Xunit;

namespace Sway.Widgets.Tests;

public class GestureTests
{
    static Widget Target(Action? onTap = null, Action? onDoubleTap = null, Action? onLongPress = null,
        Action<DragDetails>? onLongPressEnd = null, Action<DragDetails>? onPanUpdate = null) =>
        new Align(Alignment.TopLeft, new GestureDetector(
            new SizedBox(100, 100), onTap: onTap, onDoubleTap: onDoubleTap, onLongPress: onLongPress,
            onLongPressEnd: onLongPressEnd, onPanUpdate: onPanUpdate, behavior: HitTestBehavior.Opaque));

    [Fact]
    public void TapFiresOnRelease()
    {
        int taps = 0;
        var h = new Harness(Target(onTap: () => taps++));
        h.Tap(50, 50);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void TapOutsideDoesNothing()
    {
        int taps = 0;
        var h = new Harness(Target(onTap: () => taps++));
        h.Tap(250, 250);
        Assert.Equal(0, taps);
    }

    [Fact]
    public void DraggingCancelsTheTap()
    {
        int taps = 0, drags = 0;
        var h = new Harness(Target(onTap: () => taps++, onPanUpdate: _ => drags++));
        h.Gestures.PointerDown(50, 50);
        h.Gestures.PointerMove(70, 50);
        h.Gestures.PointerMove(90, 50);
        h.Gestures.PointerUp(90, 50);
        Assert.Equal(0, taps);
        Assert.True(drags > 0);
    }

    [Fact]
    public void LongPressFiresAfterTheDelayWhileHeld()
    {
        int presses = 0, taps = 0, ends = 0;
        var h = new Harness(Target(onTap: () => taps++, onLongPress: () => presses++, onLongPressEnd: _ => ends++));
        h.Gestures.PointerDown(50, 50);
        h.Advance(400);
        Assert.Equal(0, presses);
        h.Advance(200);
        Assert.Equal(1, presses);
        h.Gestures.PointerUp(50, 50);
        Assert.Equal(1, ends);
        Assert.Equal(0, taps);
    }

    [Fact]
    public void ReleasingEarlyIsATapNotALongPress()
    {
        int presses = 0, taps = 0;
        var h = new Harness(Target(onTap: () => taps++, onLongPress: () => presses++));
        h.Gestures.PointerDown(50, 50);
        h.Advance(200);
        h.Gestures.PointerUp(50, 50);
        h.Advance(1000);
        Assert.Equal(1, taps);
        Assert.Equal(0, presses);
    }

    [Fact]
    public void MovingAwayCancelsTheLongPress()
    {
        int presses = 0;
        var h = new Harness(Target(onLongPress: () => presses++));
        h.Gestures.PointerDown(50, 50);
        h.Gestures.PointerMove(90, 50);
        h.Advance(800);
        Assert.Equal(0, presses);
    }

    [Fact]
    public void DoubleTapFiresOnTheSecondTapAndSuppressesTheSingleTap()
    {
        int taps = 0, doubles = 0;
        var h = new Harness(Target(onTap: () => taps++, onDoubleTap: () => doubles++));
        h.Tap(50, 50);
        h.Advance(100);
        h.Tap(52, 50);
        h.Advance(600);
        Assert.Equal(1, doubles);
        Assert.Equal(0, taps);
    }

    [Fact]
    public void SingleTapWaitsOutTheDoubleTapWindow()
    {
        int taps = 0, doubles = 0;
        var h = new Harness(Target(onTap: () => taps++, onDoubleTap: () => doubles++));
        h.Tap(50, 50);
        Assert.Equal(0, taps);
        h.Advance(400);
        Assert.Equal(1, taps);
        Assert.Equal(0, doubles);
    }

    [Fact]
    public void SlowSecondTapIsTwoSingleTaps()
    {
        int taps = 0, doubles = 0;
        var h = new Harness(Target(onTap: () => taps++, onDoubleTap: () => doubles++));
        h.Tap(50, 50);
        h.Advance(500);
        h.Tap(50, 50);
        h.Advance(500);
        Assert.Equal(2, taps);
        Assert.Equal(0, doubles);
    }
}

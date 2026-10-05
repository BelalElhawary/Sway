using Xunit;

namespace Sway.Widgets.Tests;

public class ScrollingTests
{
    // 400x300 window, 1000px of content: max scroll extent is 700.
    static (Harness harness, ScrollController controller) Scroller()
    {
        var controller = new ScrollController();
        var h = new Harness(new SingleChildScrollView(new SizedBox(400, 1000), controller: controller));
        return (h, controller);
    }

    [Fact]
    public void ReportsScrollExtents()
    {
        var (_, c) = Scroller();
        Assert.Equal(700, c.MaxScrollExtent);
        Assert.Equal(0, c.Offset);
    }

    [Fact]
    public void WheelScrollsSmoothlyToTheTarget()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Pump();
        Assert.True(c.Offset < 100, "starts from the current position");
        h.Advance(60);
        float midway = c.Offset;
        Assert.InRange(midway, 1, 99);
        h.Advance(400);
        Assert.Equal(100, c.Offset, 1);
    }

    [Fact]
    public void RepeatedNotchesAccumulate()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Advance(40);
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Advance(600);
        Assert.Equal(300, c.Offset, 1);
    }

    [Fact]
    public void WheelClampsAtTheEnd()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerScroll(200, 150, 0, 5000);
        h.Advance(600);
        Assert.Equal(700, c.Offset, 1);
    }

    [Fact]
    public void WheelAtTheEdgeFallsThroughToTheOuterScrollable()
    {
        var outer = new ScrollController();
        var inner = new ScrollController();
        var h = new Harness(new SingleChildScrollView(
            new Column(new Widget[]
            {
                new SizedBox(400, 300, new SingleChildScrollView(new SizedBox(400, 600), controller: inner)),
                new SizedBox(400, 600),
            }), controller: outer));

        // The inner list is at its start, so scrolling up does nothing, and scrolling down moves only the inner one.
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Advance(400);
        Assert.Equal(100, inner.Offset, 1);
        Assert.Equal(0, outer.Offset, 1);

        h.Gestures.PointerScroll(200, 150, 0, 5000);
        h.Advance(400);
        Assert.Equal(inner.MaxScrollExtent, inner.Offset, 1);
        h.Gestures.PointerScroll(200, 150, 0, 100);
        h.Advance(400);
        Assert.True(outer.Offset > 0, "the outer scrollable takes over once the inner one is exhausted");
    }

    [Fact]
    public void DraggingTheThumbScrollsProportionally()
    {
        var (h, c) = Scroller();
        // Thumb length = 300 * 300 / 1000 = 90, so the thumb travels 300 - 90 - 4 = 206px for 700px of scroll.
        h.Gestures.PointerDown(395, 20);
        h.Gestures.PointerMove(395, 20 + 103);
        h.Pump();
        Assert.Equal(350, c.Offset, 0);
        h.Gestures.PointerUp(395, 123);
        Assert.False(c.Position.ScrollbarActive);
    }

    [Fact]
    public void DraggingTheThumbDoesNotAlsoDragTheContent()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerDown(395, 20);
        // The thumb moves 20px while the content drag would have moved it by the pointer delta (20px) the other way.
        h.Gestures.PointerMove(395, 40);
        h.Gestures.PointerMove(395, 60);
        h.Pump();
        float expected = 40 / 206f * 700;
        Assert.Equal(expected, c.Offset, 0);
        h.Gestures.PointerUp(395, 60);
    }

    [Fact]
    public void ClickingTheTrackPagesTowardTheClick()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerDown(395, 250);
        h.Gestures.PointerUp(395, 250);
        h.Advance(500);
        Assert.Equal(270, c.Offset, 1);
    }

    [Fact]
    public void ContentUnderTheGutterIsNotHit()
    {
        int taps = 0;
        var h = new Harness(new SingleChildScrollView(
            new GestureDetector(new SizedBox(400, 1000), onTap: () => taps++, behavior: HitTestBehavior.Opaque)));
        h.Tap(200, 100);
        Assert.Equal(1, taps);
        h.Tap(395, 280);
        h.Advance(500);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void LazyListScrollbarCanBeDragged()
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(100, (_, i) => new SizedBox(height: 50, child: new Text("Item " + i)),
            itemExtent: 50, controller: controller));
        // 5000px of content in a 300px viewport: thumb = 18 -> clamped to 24, travel 272px for 4700px.
        h.Gestures.PointerDown(395, 10);
        h.Gestures.PointerMove(395, 10 + 136);
        h.Pump();
        Assert.InRange(controller.Offset, 2300, 2400);
        h.Gestures.PointerUp(395, 146);
    }

    [Fact]
    public void PressingTheContentDoesNotScroll()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerDown(200, 150);
        h.Gestures.PointerMove(200, 250);
        h.Gestures.PointerUp(200, 250);
        h.Advance(400);
        Assert.Equal(0, c.Offset, 1);
    }

    [Fact]
    public void DraggingTheGutterThumbScrolls()
    {
        var (h, c) = Scroller();
        h.Gestures.PointerDown(396, 6);
        h.Gestures.PointerMove(396, 60);
        h.Gestures.PointerUp(396, 60);
        h.Advance(400);
        Assert.True(c.Offset > 100, $"offset was {c.Offset}");
    }
}

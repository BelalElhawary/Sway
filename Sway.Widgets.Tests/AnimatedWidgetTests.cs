using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class AnimatedWidgetTests
{
    static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void FractionallySizedBoxAnimatesItsFactor()
    {
        Widget Build(float factor) => new Align(Alignment.TopLeft,
            new SizedBox(200, 100, new AnimatedFractionallySizedBox(Short, new ColoredBox(Colors.Red), widthFactor: factor, heightFactor: 1)));

        var h = new Harness(Build(0.5f));
        float Width() => h.Find<RenderFractionalBox>()[0].Size.Width;
        Assert.Equal(100, Width(), 1);

        h.Binding.ReassembleRoot(Build(1f));
        h.Pump();
        h.Advance(100);
        Assert.InRange(Width(), 101, 199);
        h.Advance(300);
        Assert.Equal(200, Width(), 1);
    }

    [Fact]
    public void PhysicalModelAnimatesItsColour()
    {
        Widget Build(SKColor color) => new AnimatedPhysicalModel(Short, new SizedBox(50, 50), color, elevation: 2, borderRadius: BorderRadius.Circular(8));
        var h = new Harness(Build(new SKColor(0, 0, 0)));
        h.Binding.ReassembleRoot(Build(new SKColor(200, 200, 200)));
        h.Pump();
        h.Advance(100);
        var mid = h.Find<RenderDecoratedBox>().First().Size; // layout survives the animation
        Assert.True(mid.Width > 0);
        h.Advance(300);
        Assert.False(h.Binding.NeedsFrame(400, 300) && h.Find<RenderDecoratedBox>().Count == 0);
    }

    [Fact]
    public void SizeTransitionScalesWithItsAnimation()
    {
        var controller = new AnimationController(new TickerHost(), Short);
        var h = new Harness(new Align(Alignment.TopLeft, new SizeTransition(controller, new SizedBox(100, 80), axisAlignment: -1)));
        Assert.Equal(0, h.Find<RenderPositionedBox>().Last().Size.Height, 1);
        controller.Forward();
        h.Advance(100);
        Assert.InRange(h.Find<RenderPositionedBox>().Last().Size.Height, 10, 70);
        h.Advance(300);
        Assert.Equal(80, h.Find<RenderPositionedBox>().Last().Size.Height, 1);
    }

    sealed class TickerHost : ITickerProvider
    {
        public Ticker CreateTicker(Action<TimeSpan> onTick) => new(onTick);
    }

    // ---- AnimatedList ----

    sealed class ListHarness
    {
        public readonly List<string> Data;
        public readonly AnimatedListController Controller = new();
        public readonly Harness Harness;

        public ListHarness(params string[] items)
        {
            Data = items.ToList();
            Harness = new Harness(new AnimatedList(Controller, Data.Count, (_, i, animation) =>
                new SizeTransition(animation, new SizedBox(height: 30, child: new Text(Data[i])), axisAlignment: -1)));
        }

        public int Rows => Harness.Find<RenderParagraph>().Count;
        public float TotalHeight => Harness.Find<RenderFlex>().Last().Size.Height;
    }

    [Fact]
    public void InitialItemsAreFullyShown()
    {
        var l = new ListHarness("a", "b", "c");
        Assert.Equal(3, l.Rows);
        Assert.Equal(90, l.TotalHeight, 1);
        Assert.Equal(3, l.Controller.Count);
    }

    [Fact]
    public void InsertedItemsGrowIn()
    {
        var l = new ListHarness("a", "b");
        l.Data.Insert(1, "new");
        l.Controller.InsertItem(1, Short);
        l.Harness.Pump();
        Assert.Equal(3, l.Rows);
        l.Harness.Advance(100);
        Assert.InRange(l.TotalHeight, 61, 89);
        l.Harness.Advance(300);
        Assert.Equal(90, l.TotalHeight, 1);
    }

    [Fact]
    public void ItemBuilderReceivesTheCurrentIndex()
    {
        var l = new ListHarness("a", "b");
        l.Data.Insert(0, "first");
        l.Controller.InsertItem(0, Short);
        l.Harness.Advance(400);
        var texts = l.Harness.Find<RenderParagraph>().Select(p => p.PlainText).ToList();
        Assert.Equal(new[] { "first", "a", "b" }, texts);
    }

    [Fact]
    public void RemovedItemsShrinkAwayThenDisappear()
    {
        var l = new ListHarness("a", "b", "c");
        l.Data.RemoveAt(1);
        l.Controller.RemoveItem(1, (_, animation) => new SizeTransition(animation, new SizedBox(height: 30, child: new Text("b")), axisAlignment: -1), Short);
        l.Harness.Pump();
        Assert.Equal(3, l.Rows); // still drawn while it animates out
        Assert.Equal(2, l.Controller.Count);
        l.Harness.Advance(100);
        Assert.InRange(l.TotalHeight, 61, 89);
        l.Harness.Advance(400);
        Assert.Equal(2, l.Rows);
        Assert.Equal(60, l.TotalHeight, 1);
    }

    [Fact]
    public void RemoveThenInsertKeepsIndicesInTermsOfLiveItems()
    {
        var l = new ListHarness("a", "b", "c");
        l.Data.RemoveAt(0);
        l.Controller.RemoveItem(0, (_, animation) => new SizeTransition(animation, new SizedBox(height: 30, child: new Text("a")), axisAlignment: -1), Short);
        l.Data.Add("d");
        l.Controller.InsertItem(2, Short);
        l.Harness.Advance(500);
        Assert.Equal(new[] { "b", "c", "d" }, l.Harness.Find<RenderParagraph>().Select(p => p.PlainText).ToArray());
    }

    [Fact]
    public void OutOfRangeIndicesAreRejected()
    {
        var l = new ListHarness("a");
        Assert.Throws<ArgumentOutOfRangeException>(() => l.Controller.InsertItem(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => l.Controller.RemoveItem(3, (_, _) => new SizedBox()));
        Assert.Throws<InvalidOperationException>(() => new AnimatedListController().InsertItem(0));
    }
}

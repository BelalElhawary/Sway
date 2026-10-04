using Xunit;

namespace Sway.Widgets.Tests;

public class VariableListTests
{
    // Item heights cycle 20, 40, 60, 80: average 50, so 100 items are 5000px tall.
    static float HeightOf(int i) => 20 + (i % 4) * 20;

    static (Harness harness, ScrollController controller) List(int count = 100)
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(count, (_, i) => new ColoredBox(Colors.Blue, new SizedBox(height: HeightOf(i))), controller: controller));
        return (h, controller);
    }

    static Dictionary<int, float> Positions(Harness h) =>
        h.Find<RenderLazyViewport>()[0].Children.ToDictionary(
            c => ((LazyListParentData)c.ParentData!).Index, c => ((LazyListParentData)c.ParentData!).Offset.Dy);

    static void Settle(Harness h) { for (int i = 0; i < 4; i++) h.Pump(); }

    [Fact]
    public void ItemsAreStackedByTheirOwnHeights()
    {
        var (h, _) = List();
        var pos = Positions(h);
        Assert.Equal(0, pos[0]);
        Assert.Equal(20, pos[1]);
        Assert.Equal(60, pos[2]);
        Assert.Equal(120, pos[3]);
        Assert.Equal(200, pos[4]);
    }

    [Fact]
    public void OnlyTheVisibleRangeIsBuilt()
    {
        var (h, _) = List(10000);
        var count = h.Find<RenderLazyViewport>()[0].Children.Count;
        Assert.InRange(count, 5, 40);
    }

    [Fact]
    public void ItemsFillTheViewportWithoutGaps()
    {
        var (h, c) = List();
        foreach (float offset in new[] { 0f, 137f, 1000f, 2500f, 4000f })
        {
            c.JumpTo(offset);
            Settle(h);
            var pos = Positions(h);
            float covered = pos.Min(kv => kv.Value);
            float end = pos.Max(kv => kv.Value + HeightOf(kv.Key));
            Assert.True(covered <= 0.5f, $"offset {offset}: top gap {covered}");
            Assert.True(end >= 299.5f, $"offset {offset}: bottom gap, ends at {end}");
            // Neighbouring items touch exactly.
            foreach (var (index, y) in pos)
                if (pos.TryGetValue(index + 1, out var next)) Assert.Equal(y + HeightOf(index), next, 1);
        }
    }

    [Fact]
    public void ScrollingToTheEndAlignsTheLastItemWithTheBottom()
    {
        var (h, c) = List();
        // The first jump lands on the estimated end; measuring the last items refines it, so keep going to the end.
        for (int i = 0; i < 5; i++)
        {
            c.JumpTo(1e6f);
            Settle(h);
        }
        var pos = Positions(h);
        Assert.True(pos.ContainsKey(99));
        Assert.Equal(300, pos[99] + HeightOf(99), 1);
        Assert.Equal(0, c.Offset - c.MaxScrollExtent, 1);
    }

    [Fact]
    public void ScrollingUpIntoUnmeasuredItemsDoesNotMoveVisibleContent()
    {
        var (h, c) = List(1000);
        c.JumpTo(20000);
        Settle(h);

        // Step upward through items that have never been measured; whatever was on screen must slide by exactly the step.
        for (int step = 0; step < 6; step++)
        {
            var before = Positions(h);
            var anchor = before.Where(kv => kv.Value >= 0).OrderBy(kv => kv.Value).First();
            const float distance = 150;
            c.JumpTo(c.Offset - distance);
            Settle(h);
            var after = Positions(h);
            Assert.True(after.ContainsKey(anchor.Key), $"step {step}: the anchor item is still built");
            Assert.Equal(anchor.Value + distance, after[anchor.Key], 0);
        }
    }

    [Fact]
    public void WorksOnTheHorizontalAxis()
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(50, (_, i) => new SizedBox(width: HeightOf(i), child: new ColoredBox(Colors.Red)),
            scrollDirection: Axis.Horizontal, controller: controller));
        var pos = h.Find<RenderLazyViewport>()[0].Children.ToDictionary(
            c => ((LazyListParentData)c.ParentData!).Index, c => ((LazyListParentData)c.ParentData!).Offset.Dx);
        Assert.Equal(0, pos[0]);
        Assert.Equal(20, pos[1]);
        Assert.Equal(60, pos[2]);
        Assert.True(controller.MaxScrollExtent > 0);
    }

    [Fact]
    public void FixedExtentStillUsesUniformItems()
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(100, (_, i) => new ColoredBox(Colors.Blue), itemExtent: 40, controller: controller));
        Assert.Equal(100 * 40 - 300, controller.MaxScrollExtent);
        var pos = Positions(h);
        Assert.Equal(40, pos[1]);
    }

    [Fact]
    public void ChangingTheItemCountKeepsWorking()
    {
        var (h, c) = List(100);
        c.JumpTo(3000);
        Settle(h);
        h.Binding.ReassembleRoot(ListView.Builder(10, (_, i) => new ColoredBox(Colors.Blue, new SizedBox(height: HeightOf(i))), controller: c));
        Settle(h);
        Assert.InRange(c.Offset, 0, c.MaxScrollExtent);
        Assert.Equal(0, h.Find<RenderLazyViewport>()[0].Children.Count(ch => ((LazyListParentData)ch.ParentData!).Index >= 10));
    }
}

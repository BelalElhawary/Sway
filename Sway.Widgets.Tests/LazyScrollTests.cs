using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimizations 3.1 / 3.2: O(log n) offsets in variable lists, and scrolling inside the built range without a layout pass.</summary>
public class LazyScrollTests
{
    static float HeightOf(int i) => 20 + (i % 4) * 20;

    static (Harness harness, ScrollController controller, RenderLazyViewport viewport) Variable(int count)
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(count, (_, i) => new ColoredBox(Colors.Blue, new SizedBox(height: HeightOf(i))), controller: controller));
        return (h, controller, h.Find<RenderLazyViewport>()[0]);
    }

    static (Harness harness, ScrollController controller, RenderLazyViewport viewport) Fixed(int count, float extent = 30)
    {
        var controller = new ScrollController();
        var h = new Harness(ListView.Builder(count, (_, i) => new ColoredBox(Colors.Blue, new SizedBox()), itemExtent: extent, controller: controller));
        return (h, controller, h.Find<RenderLazyViewport>()[0]);
    }

    static Dictionary<int, float> Positions(RenderLazyViewport v) =>
        v.Children.ToDictionary(c => ((LazyListParentData)c.ParentData!).Index, c => ((LazyListParentData)c.ParentData!).Offset.Dy);

    static void Settle(Harness h) { for (int i = 0; i < 4; i++) h.Pump(); }

    static void AssertCoversViewport(Dictionary<int, float> pos, Func<int, float> height, float viewport, string context)
    {
        Assert.True(pos.Min(kv => kv.Value) <= 0.5f, $"{context}: top gap");
        Assert.True(pos.Max(kv => kv.Value + height(kv.Key)) >= viewport - 0.5f, $"{context}: bottom gap");
        foreach (var (index, y) in pos)
            if (pos.TryGetValue(index + 1, out var next)) Assert.Equal(y + height(index), next, 1);
    }

    [Fact]
    public void PrefixOffsetsMatchALinearScanAfterManyMeasurements()
    {
        var (h, c, v) = Variable(5000);
        var rng = new Random(7);
        for (int i = 0; i < 30; i++) { c.JumpTo(rng.Next(0, 240000)); Settle(h); }

        foreach (int index in new[] { 0, 1, 7, 500, 1234, 2499, 4999, 5000 })
        {
            double linear = 0;
            for (int j = 0; j < index; j++) linear += v.ExtentOf(j);
            Assert.Equal(linear, v.PrefixOffset(index, 0), Math.Max(0.01, linear * 1e-5));
        }
    }

    [Fact]
    public void ScrollingInsideTheBuiltRangeOfAVariableListSkipsLayout()
    {
        var (h, c, v) = Variable(1000);
        c.JumpTo(500);
        Settle(h);
        var before = Positions(v);
        int passes = v.LayoutPasses;

        c.JumpTo(c.Offset + 1);
        h.Pump();

        Assert.Equal(passes, v.LayoutPasses);
        var after = Positions(v);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        foreach (var (index, y) in before) Assert.Equal(y - 1, after[index], 2);
    }

    [Fact]
    public void ScrollingInsideTheBuiltRangeOfAFixedListSkipsLayout()
    {
        var (h, c, v) = Fixed(1000);
        c.JumpTo(605);
        Settle(h);
        var before = Positions(v);
        int passes = v.LayoutPasses;

        c.JumpTo(c.Offset + 1);
        h.Pump();

        Assert.Equal(passes, v.LayoutPasses);
        foreach (var (index, y) in before) Assert.Equal(y - 1, Positions(v)[index], 2);
    }

    [Fact]
    public void ScrollingPastTheBuiltRangeStillBuildsNewItems()
    {
        var (h, c, v) = Variable(1000);
        Settle(h);
        int passes = v.LayoutPasses;
        c.JumpTo(3000);
        Settle(h);
        Assert.True(v.LayoutPasses > passes);
        Assert.True(Positions(v).Keys.Min() > 30, "items far from the start should be built after the jump");
    }

    [Fact]
    public void SmallStepsKeepAVariableListGaplessWithFewerLayouts()
    {
        var (h, c, v) = Variable(2000);
        Settle(h);
        int passes = v.LayoutPasses;
        const int steps = 200;
        for (int i = 0; i < steps; i++)
        {
            c.JumpTo(c.Offset + 3);
            h.Pump();
            AssertCoversViewport(Positions(v), HeightOf, 300, $"step {i}");
        }
        Assert.True(v.LayoutPasses - passes < steps / 2, $"{v.LayoutPasses - passes} layouts for {steps} steps");
    }

    [Fact]
    public void SmallStepsKeepAFixedListGaplessWithFewerLayouts()
    {
        var (h, c, v) = Fixed(5000);
        Settle(h);
        int passes = v.LayoutPasses;
        const int steps = 300;
        for (int i = 0; i < steps; i++)
        {
            c.JumpTo(c.Offset + 3);
            h.Pump();
            AssertCoversViewport(Positions(v), _ => 30, 300, $"step {i}");
        }
        Assert.True(v.LayoutPasses - passes < steps / 2, $"{v.LayoutPasses - passes} layouts for {steps} steps");
    }

    [Fact]
    public void ScrollingBackwardsInSmallStepsStaysGapless()
    {
        var (h, c, v) = Variable(2000);
        c.JumpTo(4000);
        Settle(h);
        for (int i = 0; i < 150; i++)
        {
            c.JumpTo(c.Offset - 4);
            h.Pump();
            AssertCoversViewport(Positions(v), HeightOf, 300, $"step {i}");
        }
    }
}

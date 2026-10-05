using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class ImplicitAnimationTests
{
    static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);
    static readonly SKColor Red = new(255, 0, 0), Green = new(0, 255, 0), Blue = new(0, 0, 255);

    // ---- gradients ----

    [Fact]
    public void GradientsWithDifferentStopCountsBlend()
    {
        var two = new LinearGradient([Red, Blue]);
        var three = new LinearGradient([Red, Green, Blue]);
        var mid = (LinearGradient)Lerps.Gradient(two, three, 0.5f)!;
        Assert.Equal(3, mid.ColorList.Count);
        // Halfway between a red-to-blue ramp and one that passes through green: the middle stop is half green over the blend.
        Assert.Equal(new SKColor(63, 127, 63), mid.ColorList[1].WithAlpha(255), new ColorComparer(2));
        Assert.Equal(0f, mid.Stops![0]);
        Assert.Equal(1f, mid.Stops[^1]);
    }

    [Fact]
    public void GradientEndpointsMatchTheirSources()
    {
        var a = new LinearGradient([Red, Blue]);
        var b = new LinearGradient([Green, Red, Blue, Green]);
        var atStart = (LinearGradient)Lerps.Gradient(a, b, 0)!;
        Assert.Equal(Red, atStart.ColorList[0]);
        Assert.Equal(Blue, atStart.ColorList[^1]);
        var atEnd = (LinearGradient)Lerps.Gradient(a, b, 1)!;
        Assert.Equal(Green, atEnd.ColorList[0]);
        Assert.Equal(Green, atEnd.ColorList[^1]);
    }

    [Fact]
    public void GradientsWithExplicitStopsBlend()
    {
        var a = new LinearGradient([Red, Blue], stops: [0f, 0.5f]);
        var b = new LinearGradient([Red, Blue], stops: [0.5f, 1f]);
        var mid = (LinearGradient)Lerps.Gradient(a, b, 0.5f)!;
        Assert.All(mid.Stops!, stop => Assert.InRange(stop, 0f, 1f));
        Assert.Equal(mid.Stops!.Count, mid.ColorList.Count);
        Assert.True(mid.Stops.Zip(mid.Stops.Skip(1)).All(p => p.First <= p.Second), "stops stay sorted");
    }

    [Fact]
    public void RadialAndSweepGradientsBlend()
    {
        Assert.IsType<RadialGradient>(Lerps.Gradient(new RadialGradient([Red, Blue]), new RadialGradient([Green, Red, Blue]), 0.5f));
        Assert.IsType<SweepGradient>(Lerps.Gradient(new SweepGradient([Red, Blue]), new SweepGradient([Green, Red, Blue]), 0.5f));
    }

    [Fact]
    public void DifferentGradientKindsStillSwitchHalfway()
    {
        var linear = new LinearGradient([Red, Blue]);
        var radial = new RadialGradient([Red, Blue]);
        Assert.Same(linear, Lerps.Gradient(linear, radial, 0.3f));
        Assert.Same(radial, Lerps.Gradient(linear, radial, 0.7f));
    }

    sealed class ColorComparer(int tolerance) : IEqualityComparer<SKColor>
    {
        public bool Equals(SKColor a, SKColor b) =>
            Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance && Math.Abs(a.Blue - b.Blue) <= tolerance;
        public int GetHashCode(SKColor c) => 0;
    }

    // ---- TweenAnimationBuilder ----

    [Fact]
    public void OneTweenCanBackTwoBuilders()
    {
        var shared = new FloatTween(0, 100);
        float? a = null, b = null;
        Widget Build(float target) => new Column(
        [
            new TweenAnimationBuilder<float>(shared, Short, (_, v, __) => { a = v; return new SizedBox(); }),
            new TweenAnimationBuilder<float>(shared, Short, (_, v, __) => { b = v; return new SizedBox(); }),
        ]);
        var h = new Harness(Build(100));
        h.Advance(500);
        Assert.Equal(0f, shared.Begin);
        Assert.Equal(100f, shared.End);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RetargetingStartsFromTheValueOnScreen()
    {
        float? seen = null;
        Widget Build(float end) => new TweenAnimationBuilder<float>(new FloatTween(0, end), Short, (_, v, __) => { seen = v; return new SizedBox(); });
        var h = new Harness(Build(100));
        h.Advance(100);
        float partway = seen!.Value;
        Assert.InRange(partway, 1, 99);
        h.Binding.ReassembleRoot(Build(0));
        h.Pump();
        Assert.InRange(seen!.Value, partway - 5, partway + 5);
        h.Advance(500);
        Assert.Equal(0f, seen!.Value, 1);
    }

    // ---- directional alignment ----

    static Offset ChildOffset(Harness h) => ((BoxParentData)h.Find<RenderBox>().OfType<RenderPositionedBox>().First().Child!.ParentData!).Offset;

    [Fact]
    public void AnimatedAlignResolvesDirectionalAlignments()
    {
        Widget Build(TextDirection d) => new Directionality(d, new AnimatedAlign(AlignmentDirectional.CenterStart, Short, new SizedBox(50, 50)));
        var ltr = new Harness(Build(TextDirection.Ltr));
        Assert.Equal(0, ChildOffset(ltr).Dx);
        var rtl = new Harness(Build(TextDirection.Rtl));
        Assert.Equal(350, ChildOffset(rtl).Dx);
    }

    [Fact]
    public void AnimatedAlignFollowsAChangeOfDirection()
    {
        Widget Build(TextDirection d) => new Directionality(d, new AnimatedAlign(AlignmentDirectional.CenterStart, Short, new SizedBox(50, 50)));
        var h = new Harness(Build(TextDirection.Ltr));
        h.Binding.ReassembleRoot(Build(TextDirection.Rtl));
        h.Pump();
        h.Advance(100);
        Assert.InRange(ChildOffset(h).Dx, 1, 349);
        h.Advance(400);
        Assert.Equal(350, ChildOffset(h).Dx, 1);
    }

    [Fact]
    public void AnimatedContainerAcceptsDirectionalAlignment()
    {
        var h = new Harness(new Directionality(TextDirection.Rtl,
            new AnimatedContainer(Short, alignment: AlignmentDirectional.CenterStart, child: new SizedBox(50, 50))));
        var aligned = h.Find<RenderPositionedBox>().First();
        Assert.Equal(350, ((BoxParentData)aligned.Child!.ParentData!).Offset.Dx);
    }

    [Fact]
    public void AnimatedPositionedDirectionalSwapsSidesInRtl()
    {
        Widget Build(TextDirection d) => new Directionality(d, new Stack(
            [new AnimatedPositionedDirectional(new SizedBox(), Short, start: 10, top: 20, width: 50, height: 50)], fit: StackFit.Expand));
        var ltr = new Harness(Build(TextDirection.Ltr));
        Assert.Equal(10, StackChildOffset(ltr).Dx);
        var rtl = new Harness(Build(TextDirection.Rtl));
        Assert.Equal(340, StackChildOffset(rtl).Dx);
    }

    static Offset StackChildOffset(Harness h)
    {
        var stack = h.Find<RenderStack>().Last();
        return ((BoxParentData)stack.Children[0].ParentData!).Offset;
    }
}

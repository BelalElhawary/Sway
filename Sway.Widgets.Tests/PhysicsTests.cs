using Xunit;

namespace Sway.Widgets.Tests;

public class PhysicsTests
{
    sealed class TestTickers : ITickerProvider
    {
        public Ticker CreateTicker(Action<TimeSpan> onTick) => new(onTick);
    }

    static float Settle(Simulation sim, float step = 0.002f)
    {
        float t = 0;
        while (!sim.IsDone(t) && t < 30) t += step;
        return t;
    }

    [Fact]
    public void FrictionStartsAtPositionAndVelocity()
    {
        var sim = new FrictionSimulation(0.05f, 10, 500);
        Assert.Equal(10, sim.X(0), 3);
        Assert.Equal(500, sim.Dx(0), 3);
    }

    [Fact]
    public void FrictionSlowsDownAndStopsAtTheFinalPosition()
    {
        var sim = new FrictionSimulation(0.05f, 0, 600) { Tolerance = new Tolerance(1, 1) };
        Assert.True(sim.Dx(0.5f) < sim.Dx(0));
        float t = Settle(sim);
        Assert.InRange(sim.X(t), sim.FinalX - 1, sim.FinalX + 1);
        Assert.True(sim.FinalX > 0);
    }

    [Fact]
    public void FrictionRejectsInvalidDrag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrictionSimulation(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrictionSimulation(0, 0, 1));
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(1f)]
    [InlineData(2.5f)]
    public void SpringMatchesInitialConditionsAndSettlesAtTheEnd(float ratio)
    {
        var spring = SpringDescription.WithDampingRatio(1, 200, ratio);
        var sim = new SpringSimulation(spring, 0, 100, 40);
        Assert.Equal(0, sim.X(0), 2);
        Assert.Equal(40, sim.Dx(0), 1);
        float t = Settle(sim);
        Assert.True(t < 30);
        Assert.Equal(100, sim.X(t), 1);
    }

    [Fact]
    public void UnderdampedSpringOvershoots()
    {
        var sim = new SpringSimulation(SpringDescription.WithDampingRatio(1, 200, 0.2f), 0, 100, 0);
        float max = 0;
        for (float t = 0; t < 2; t += 0.005f) max = Math.Max(max, sim.X(t));
        Assert.True(max > 105, $"peak {max}");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void CriticalAndOverdampedSpringsNeverOvershoot(float ratio)
    {
        var sim = new SpringSimulation(SpringDescription.WithDampingRatio(1, 200, ratio), 0, 100, 0);
        for (float t = 0; t < 5; t += 0.005f) Assert.True(sim.X(t) <= 100.001f, $"t={t} x={sim.X(t)}");
    }

    [Fact]
    public void SpringVelocityIsTheDerivativeOfPosition()
    {
        foreach (float ratio in new[] { 0.4f, 1f, 2f })
        {
            var sim = new SpringSimulation(SpringDescription.WithDampingRatio(1, 150, ratio), 10, 60, -25);
            const float h = 1e-3f;
            foreach (float t in new[] { 0.05f, 0.2f, 0.6f })
            {
                float numeric = (sim.X(t + h) - sim.X(t - h)) / (2 * h);
                Assert.Equal(numeric, sim.Dx(t), 0);
            }
        }
    }

    [Fact]
    public void ControllerFlingReachesTheUpperBound()
    {
        var h = new Harness(new SizedBox());
        var controller = new AnimationController(new TestTickers(), TimeSpan.FromMilliseconds(300));
        controller.Fling(2);
        Assert.Equal(AnimationStatus.Forward, controller.Status);
        h.Advance(100);
        Assert.InRange(controller.Value, 0.1f, 1f);
        h.Advance(3000);
        Assert.Equal(1f, controller.Value, 2);
        Assert.Equal(AnimationStatus.Completed, controller.Status);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void ControllerFlingWithNegativeVelocityReturnsToTheLowerBound()
    {
        var h = new Harness(new SizedBox());
        var controller = new AnimationController(new TestTickers(), TimeSpan.FromMilliseconds(300), value: 0.8f);
        controller.Fling(-3);
        Assert.Equal(AnimationStatus.Reverse, controller.Status);
        h.Advance(3000);
        Assert.Equal(0f, controller.Value, 2);
        Assert.Equal(AnimationStatus.Dismissed, controller.Status);
    }

    [Fact]
    public void ControllerStaysInsideItsBoundsWhileSpringOvershoots()
    {
        var h = new Harness(new SizedBox());
        var controller = new AnimationController(new TestTickers(), TimeSpan.FromMilliseconds(300));
        controller.Fling(5, SpringDescription.WithDampingRatio(1, 500, 0.2f));
        for (int i = 0; i < 100; i++)
        {
            h.Advance(16);
            Assert.InRange(controller.Value, 0f, 1f);
        }
    }

    [Fact]
    public void AnimateWithRunsAnySimulation()
    {
        var h = new Harness(new SizedBox());
        var controller = new AnimationController(new TestTickers(), TimeSpan.FromMilliseconds(300), upperBound: 1000);
        controller.AnimateWith(new FrictionSimulation(0.05f, 0, 800) { Tolerance = new Tolerance(0.01f, 1) });
        h.Advance(4000);
        Assert.True(controller.Value > 100);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void ScrollFlingGlidesToAStopAndCanBeInterrupted()
    {
        var controller = new ScrollController();
        var h = new Harness(new SingleChildScrollView(new ColoredBox(new SkiaSharp.SKColor(0), new SizedBox(400, 5000)), controller: controller));
        controller.Position.Fling(2000);
        h.Advance(100);
        float early = controller.Offset;
        Assert.True(early > 0);
        h.Advance(100);
        Assert.True(controller.Offset > early);
        h.Advance(5000);
        float rest = controller.Offset;
        // v0 / 3.2 px: the exponential decay's total travel.
        Assert.InRange(rest, 2000 / 3.2f - 60, 2000 / 3.2f + 60);
        h.Advance(500);
        Assert.Equal(rest, controller.Offset);

        controller.Position.Fling(2000);
        h.Advance(50);
        controller.Position.StopActivity();
        float stopped = controller.Offset;
        h.Advance(500);
        Assert.Equal(stopped, controller.Offset);
    }
}

using Xunit;

namespace Sway.Widgets.Tests;

public class CurvesTests
{
    public static IEnumerable<object[]> AllCurves() => new[]
    {
        Curves.Linear, Curves.Decelerate, Curves.Ease, Curves.EaseIn, Curves.EaseOut, Curves.EaseInOut,
        Curves.EaseInQuad, Curves.EaseOutQuad, Curves.EaseInOutQuad, Curves.EaseInCubic, Curves.EaseOutCubic,
        Curves.EaseInOutCubic, Curves.EaseInExpo, Curves.EaseOutExpo, Curves.EaseInBack, Curves.EaseOutBack,
        Curves.FastOutSlowIn, Curves.BounceIn, Curves.BounceOut, Curves.BounceInOut, Curves.ElasticIn, Curves.ElasticOut,
    }.Select(c => new object[] { c });

    [Theory, MemberData(nameof(AllCurves))]
    public void EndpointsAreFixed(Curve curve)
    {
        Assert.Equal(0f, curve.Transform(0));
        Assert.Equal(1f, curve.Transform(1));
    }

    [Theory, MemberData(nameof(AllCurves))]
    public void ProducesFiniteValues(Curve curve)
    {
        for (float t = 0.01f; t < 1; t += 0.01f)
            Assert.True(float.IsFinite(curve.Transform(t)), $"t={t}");
    }

    [Fact]
    public void LinearIsIdentity() => Assert.Equal(0.3f, Curves.Linear.Transform(0.3f));

    [Fact]
    public void EaseInStartsSlowAndEaseOutStartsFast()
    {
        Assert.True(Curves.EaseIn.Transform(0.25f) < 0.25f);
        Assert.True(Curves.EaseOut.Transform(0.25f) > 0.25f);
    }

    [Fact]
    public void EaseInOutIsSymmetricAroundTheMidpoint()
    {
        Assert.Equal(0.5f, Curves.EaseInOut.Transform(0.5f), 2);
        Assert.Equal(1 - Curves.EaseInOut.Transform(0.2f), Curves.EaseInOut.Transform(0.8f), 2);
    }

    [Fact]
    public void BackCurvesOvershoot()
    {
        Assert.True(Curves.EaseOutBack.Transform(0.8f) > 1);
        Assert.True(Curves.EaseInBack.Transform(0.2f) < 0);
    }

    [Fact]
    public void FlippedMirrorsTheCurve()
    {
        var flipped = Curves.EaseIn.Flipped;
        Assert.Equal(1 - Curves.EaseIn.Transform(0.7f), flipped.Transform(0.3f), 2);
    }

    [Fact]
    public void IntervalHoldsOutsideItsRangeAndRunsInside()
    {
        var curve = new Interval(0.25f, 0.75f);
        Assert.Equal(0f, curve.Transform(0.1f));
        Assert.Equal(1f, curve.Transform(0.9f));
        Assert.Equal(0.5f, curve.Transform(0.5f), 3);
    }

    [Fact]
    public void StepsJumpBetweenLevels()
    {
        var curve = new StepsCurve(4);
        Assert.Equal(0.25f, curve.Transform(0.3f));
        Assert.Equal(0.75f, curve.Transform(0.99f));
    }

    [Fact]
    public void ThresholdSwitchesAtItsValue()
    {
        var curve = new Threshold(0.6f);
        Assert.Equal(0f, curve.Transform(0.5f));
        Assert.Equal(1f, curve.Transform(0.6f));
    }
}

using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class LerpsTests
{
    [Fact]
    public void FloatBlendsLinearly()
    {
        Assert.Equal(10f, Lerps.Float(10, 20, 0));
        Assert.Equal(15f, Lerps.Float(10, 20, 0.5f));
        Assert.Equal(20f, Lerps.Float(10, 20, 1));
    }

    [Fact]
    public void ColorBlendsEachChannel()
    {
        var mid = Lerps.Color(new SKColor(0, 100, 200, 255), new SKColor(100, 200, 0, 255), 0.5f);
        Assert.Equal(new SKColor(50, 150, 100, 255), mid);
    }

    [Fact]
    public void ColorClampsOvershoot()
    {
        var c = Lerps.Color(new SKColor(250, 0, 0), new SKColor(255, 0, 0), 5);
        Assert.Equal(255, c.Red);
    }

    [Fact]
    public void MissingColorFadesInAndOut()
    {
        SKColor? red = new SKColor(255, 0, 0, 200);
        Assert.Null(Lerps.Color((SKColor?)null, (SKColor?)null, 0.5f));
        Assert.Equal(100, Lerps.Color(null, red, 0.5f)!.Value.Alpha);
        Assert.Equal(100, Lerps.Color(red, null, 0.5f)!.Value.Alpha);
        Assert.Equal(0, Lerps.Color(red, null, 1)!.Value.Alpha);
    }

    [Fact]
    public void GeometryBlends()
    {
        Assert.Equal(new Offset(5, 10), Lerps.Offset(new Offset(0, 0), new Offset(10, 20), 0.5f));
        Assert.Equal(new Size(15, 25), Lerps.Size(new Size(10, 20), new Size(20, 30), 0.5f));
        Assert.Equal(new EdgeInsets(1, 2, 3, 4), Lerps.EdgeInsets(EdgeInsets.Zero, new EdgeInsets(2, 4, 6, 8), 0.5f));
        Assert.Equal(new Alignment(0, 0), Lerps.Alignment(Alignment.TopLeft, Alignment.BottomRight, 0.5f));
    }

    [Fact]
    public void InfiniteConstraintsHoldUntilTheOtherSide()
    {
        var a = new BoxConstraints(0, float.PositiveInfinity, 0, 100);
        var b = new BoxConstraints(0, 200, 0, 100);
        Assert.True(float.IsPositiveInfinity(Lerps.BoxConstraints(a, b, 0.4f).MaxWidth));
        Assert.Equal(200f, Lerps.BoxConstraints(a, b, 0.6f).MaxWidth);
    }

    [Fact]
    public void BorderSideNeverGoesNegative()
    {
        var side = Lerps.BorderSide(new BorderSide(new SKColor(0, 0, 0), 2), new BorderSide(new SKColor(0, 0, 0), 0), 1.5f);
        Assert.True(side.Width >= 0);
    }

    [Fact]
    public void ShadowListsOfDifferentLengthFadeTheExtras()
    {
        var one = new[] { new BoxShadow(new SKColor(0, 0, 0, 100), new Offset(0, 2), 4, 0) };
        var result = Lerps.BoxShadows(Array.Empty<BoxShadow>(), one, 0.5f)!;
        Assert.Single(result);
        Assert.Equal(50, result[0].Color.Alpha);
        Assert.Null(Lerps.BoxShadows(null, null, 0.5f));
    }

    [Fact]
    public void FontWeightSnapsToHundreds()
    {
        var a = new TextStyle(FontWeight: 400);
        var b = new TextStyle(FontWeight: 700);
        Assert.Equal(600, Lerps.TextStyle(a, b, 0.6f).FontWeight);
    }
}

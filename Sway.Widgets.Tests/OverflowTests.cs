using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class OverflowTests
{
    static Widget Row(float child, float window) => new Align(Alignment.TopLeft, new SizedBox(window, 40,
        new Row([new SizedBox(child, 40, new ColoredBox(Colors.Blue)), new SizedBox(child, 40, new ColoredBox(Colors.Blue))])));

    [Fact]
    public void ReportsHowFarTheChildrenOverflow()
    {
        var h = new Harness(Row(80, 100));
        Assert.Equal(60, h.Find<RenderFlex>().Last().OverflowExtent, 1);
    }

    [Fact]
    public void NoOverflowWhenChildrenFit()
    {
        var h = new Harness(Row(40, 100));
        Assert.Equal(0, h.Find<RenderFlex>().Last().OverflowExtent);
    }

    [Fact]
    public void PaintsAHazardBandOnTheOverflowingEdge()
    {
        bool before = RenderFlex.PaintOverflowIndicators;
        RenderFlex.PaintOverflowIndicators = true;
        try
        {
            var h = new Harness(Row(80, 100));
            using var bitmap = h.Render();
            var px = Enumerable.Range(0, 40).Select(y => bitmap.GetPixel(96, y)).ToList();
            Assert.Contains(px, c => c.Red > 200 && c.Green > 180 && c.Blue < 50);
            Assert.Equal(Colors.Blue, bitmap.GetPixel(50, 20));
        }
        finally { RenderFlex.PaintOverflowIndicators = before; }
    }

    [Fact]
    public void CanBeSwitchedOff()
    {
        bool before = RenderFlex.PaintOverflowIndicators;
        RenderFlex.PaintOverflowIndicators = false;
        try
        {
            var h = new Harness(Row(80, 100));
            using var bitmap = h.Render();
            Assert.Equal(Colors.Blue, bitmap.GetPixel(96, 20));
        }
        finally { RenderFlex.PaintOverflowIndicators = before; }
    }
}

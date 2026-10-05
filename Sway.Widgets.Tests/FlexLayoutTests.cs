using Xunit;

namespace Sway.Widgets.Tests;

public class FlexLayoutTests
{
    static RenderConstrainedBox Box(float w, float h) =>
        new(BoxConstraints.TightFor(w, h)) { ParentData = new FlexParentData() };

    // Unbounded width, fixed 20px height: width is whatever the flex allotment forces.
    static RenderConstrainedBox FlexBox(int flex, FlexFit fit = FlexFit.Tight) =>
        new(new BoxConstraints(0, float.PositiveInfinity, 20, 20)) { ParentData = new FlexParentData { Flex = flex, Fit = fit } };

    static RenderFlex Flex(IReadOnlyList<RenderBox> children, MainAxisAlignment main = MainAxisAlignment.Start,
        CrossAxisAlignment cross = CrossAxisAlignment.Start, MainAxisSize size = MainAxisSize.Max,
        TextDirection dir = TextDirection.Ltr, float spacing = 0, Axis axis = Axis.Horizontal)
    {
        var flex = new RenderFlex(axis, main, size, cross, dir, VerticalDirection.Down, spacing);
        flex.SetChildren(children);
        return flex;
    }

    static float X(RenderBox b) => ((BoxParentData)b.ParentData!).Offset.Dx;
    static float Y(RenderBox b) => ((BoxParentData)b.ParentData!).Offset.Dy;

    [Fact]
    public void FixedChildrenPackAtTheStart()
    {
        var a = Box(30, 10); var b = Box(50, 20);
        var row = Flex(new[] { a, b });
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(new Size(200, 20), row.Size);
        Assert.Equal(0, X(a));
        Assert.Equal(30, X(b));
    }

    [Fact]
    public void MainAxisSizeMinShrinkWraps()
    {
        var row = Flex(new[] { Box(30, 10), Box(50, 20) }, size: MainAxisSize.Min);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(80, row.Size.Width);
    }

    [Fact]
    public void SpacingIsAddedBetweenChildren()
    {
        var a = Box(30, 10); var b = Box(30, 10); var c = Box(30, 10);
        var row = Flex(new[] { a, b, c }, size: MainAxisSize.Min, spacing: 5);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(100, row.Size.Width);
        Assert.Equal(35, X(b));
        Assert.Equal(70, X(c));
    }

    [Theory]
    [InlineData(MainAxisAlignment.End, 140, 170)]
    [InlineData(MainAxisAlignment.Center, 70, 100)]
    [InlineData(MainAxisAlignment.SpaceBetween, 0, 170)]
    [InlineData(MainAxisAlignment.SpaceAround, 35, 135)]
    [InlineData(MainAxisAlignment.SpaceEvenly, 46.667f, 123.333f)]
    public void MainAxisAlignmentPlacesTwoChildren(MainAxisAlignment alignment, float firstX, float secondX)
    {
        var a = Box(30, 10); var b = Box(30, 10);
        var row = Flex(new[] { a, b }, main: alignment);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(firstX, X(a), 2);
        Assert.Equal(secondX, X(b), 2);
    }

    [Fact]
    public void TightFlexChildrenShareTheRemainingSpaceByWeight()
    {
        var fixedBox = Box(40, 20); var one = FlexBox(1); var two = FlexBox(2);
        var row = Flex(new RenderBox[] { fixedBox, one, two });
        row.Layout(BoxConstraints.Loose(new Size(220, 100)));
        Assert.Equal(60, one.Size.Width, 2);
        Assert.Equal(120, two.Size.Width, 2);
        Assert.Equal(40, X(one));
        Assert.Equal(100, X(two));
    }

    [Fact]
    public void LooseFlexChildKeepsItsNaturalSize()
    {
        var loose = new RenderConstrainedBox(BoxConstraints.TightFor(30, 10))
            { ParentData = new FlexParentData { Flex = 1, Fit = FlexFit.Loose } };
        var row = Flex(new RenderBox[] { loose });
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(30, loose.Size.Width);
    }

    [Fact]
    public void CrossAxisAlignmentPositionsAcrossTheRow()
    {
        var small = Box(10, 10); var tall = Box(10, 40);
        var row = Flex(new[] { small, tall }, cross: CrossAxisAlignment.Center);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(40, row.Size.Height);
        Assert.Equal(15, Y(small));

        var end = Flex(new[] { Box(10, 10), Box(10, 40) }, cross: CrossAxisAlignment.End);
        end.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(30, Y(end.Children[0]));
    }

    [Fact]
    public void StretchGivesChildrenTheFullCrossExtent()
    {
        var child = new RenderConstrainedBox(new BoxConstraints(0, 10, 0, float.PositiveInfinity))
            { ParentData = new FlexParentData() };
        var row = Flex(new RenderBox[] { child }, cross: CrossAxisAlignment.Stretch);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(100, child.Size.Height);
    }

    [Fact]
    public void RtlRowMirrorsTheMainAxis()
    {
        var a = Box(30, 10); var b = Box(50, 10);
        var row = Flex(new[] { a, b }, dir: TextDirection.Rtl);
        row.Layout(BoxConstraints.Loose(new Size(200, 100)));
        Assert.Equal(170, X(a));
        Assert.Equal(120, X(b));
    }

    [Fact]
    public void ColumnLaysOutVertically()
    {
        var a = Box(10, 30); var b = Box(20, 50);
        var col = Flex(new[] { a, b }, axis: Axis.Vertical, size: MainAxisSize.Min);
        col.Layout(BoxConstraints.Loose(new Size(100, 200)));
        Assert.Equal(new Size(20, 80), col.Size);
        Assert.Equal(30, Y(b));
    }

    [Fact]
    public void UnboundedMainAxisUsesNaturalSizes()
    {
        var flexChild = new RenderConstrainedBox(BoxConstraints.TightFor(25, 10))
            { ParentData = new FlexParentData { Flex = 1, Fit = FlexFit.Loose } };
        var row = Flex(new[] { (RenderBox)Box(30, 10), flexChild });
        row.Layout(new BoxConstraints(0, float.PositiveInfinity, 0, 100));
        Assert.Equal(55, row.Size.Width);
    }

    [Fact]
    public void OverflowKeepsChildrenAtTheirNaturalOffsets()
    {
        var a = Box(80, 10); var b = Box(80, 10);
        var row = Flex(new[] { a, b });
        row.Layout(BoxConstraints.Loose(new Size(100, 50)));
        Assert.Equal(100, row.Size.Width);
        Assert.Equal(80, X(b));
    }
}

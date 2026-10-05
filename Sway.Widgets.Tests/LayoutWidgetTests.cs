using Xunit;

namespace Sway.Widgets.Tests;

public class LayoutWidgetTests
{
    [Fact]
    public void LayoutBuilderReceivesTheParentConstraints()
    {
        BoxConstraints? seen = null;
        var h = new Harness(new Align(Alignment.TopLeft, new SizedBox(200, 100, new LayoutBuilder((_, c) =>
        {
            seen = c;
            return new SizedBox();
        }))));
        Assert.Equal(BoxConstraints.Tight(new Size(200, 100)), seen);
    }

    [Fact]
    public void LayoutBuilderBuildsDifferentChildrenForDifferentSizes()
    {
        Widget Build(float width) => new Align(Alignment.TopLeft, new SizedBox(width, 100, new LayoutBuilder((_, c) =>
            c.MaxWidth < 300 ? new ColoredBox(Colors.Red, new SizedBox(width: 10, height: 10)) : new Text("wide"))));

        var narrow = new Harness(Build(200));
        Assert.Empty(narrow.Find<RenderParagraph>());
        var wide = new Harness(Build(350));
        Assert.Single(wide.Find<RenderParagraph>());
    }

    [Fact]
    public void LayoutBuilderRebuildsWhenTheBuilderChanges()
    {
        int builds = 0;
        Widget Build(string label) => new LayoutBuilder((_, c) => { builds++; return new Text(label); });
        var h = new Harness(Build("a"));
        Assert.Equal(1, builds);
        h.Pump();
        Assert.Equal(1, builds);
        h.Binding.ReassembleRoot(Build("b"));
        h.Pump();
        Assert.Equal(2, builds);
    }

    [Fact]
    public void OverflowBoxLetsTheChildExceedTheParent()
    {
        var h = new Harness(new Align(Alignment.TopLeft, new SizedBox(100, 100,
            new OverflowBox(new SizedBox(300, 50), alignment: Alignment.TopLeft, maxWidth: 300))));
        var boxes = h.Find<RenderOverflowBox>();
        Assert.Single(boxes);
        Assert.Equal(new Size(100, 100), boxes[0].Size);
        Assert.Equal(300, boxes[0].Child!.Size.Width);
    }

    [Fact]
    public void OverflowBoxAlignsTheOverflowingChild()
    {
        var h = new Harness(new Align(Alignment.TopLeft, new SizedBox(100, 100,
            new OverflowBox(new SizedBox(300, 50), minWidth: 300, maxWidth: 300, minHeight: 0))));
        var box = h.Find<RenderOverflowBox>()[0];
        var offset = ((BoxParentData)box.Child!.ParentData!).Offset;
        Assert.Equal(-100, offset.Dx);
        Assert.Equal(25, offset.Dy);
    }

    [Fact]
    public void SizedOverflowBoxHasItsOwnSize()
    {
        var h = new Harness(new Align(Alignment.TopLeft,
            new SizedOverflowBox(new Size(40, 30), new SizedBox(100, 100), alignment: Alignment.TopLeft)));
        var box = h.Find<RenderSizedOverflowBox>()[0];
        Assert.Equal(new Size(40, 30), box.Size);
        Assert.Equal(new Size(100, 100), box.Child!.Size);
    }

    sealed class TwoPaneDelegate : MultiChildLayoutDelegate
    {
        public override void PerformLayout(Size size)
        {
            var side = LayoutChild("side", BoxConstraints.TightFor(width: 80, height: size.Height));
            PositionChild("side", Offset.Zero);
            LayoutChild("main", BoxConstraints.Tight(new Size(size.Width - side.Width, size.Height)));
            PositionChild("main", new Offset(side.Width, 0));
        }
    }

    [Fact]
    public void CustomMultiChildLayoutPositionsChildrenById()
    {
        var h = new Harness(new CustomMultiChildLayout(new TwoPaneDelegate(), new Widget[]
        {
            new LayoutId("main", new ColoredBox(Colors.Blue)),
            new LayoutId("side", new ColoredBox(Colors.Red)),
        }));
        var layout = h.Find<RenderCustomMultiChildLayout>()[0];
        Assert.Equal(new Size(400, 300), layout.Size);
        var side = layout.Children.Single(c => ((MultiChildLayoutParentData)c.ParentData!).Id!.Equals("side"));
        var main = layout.Children.Single(c => ((MultiChildLayoutParentData)c.ParentData!).Id!.Equals("main"));
        Assert.Equal(new Size(80, 300), side.Size);
        Assert.Equal(new Size(320, 300), main.Size);
        Assert.Equal(80, ((BoxParentData)main.ParentData!).Offset.Dx);
    }

    [Fact]
    public void CustomMultiChildLayoutRejectsChildrenWithoutAnId()
    {
        Assert.ThrowsAny<Exception>(() => new Harness(new CustomMultiChildLayout(new TwoPaneDelegate(), new Widget[]
        {
            new ColoredBox(Colors.Blue),
        })));
    }
}

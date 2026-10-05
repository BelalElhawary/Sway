using Xunit;

namespace Sway.Widgets.Tests;

public class GridViewTests
{
    static List<(int index, Offset offset, Size size)> Cells(Harness h) =>
        h.Find<RenderLazyViewport>()[0].Children
            .SelectMany(row => ((RenderBoxContainer)Descend(row)).Children.Select((cell, i) => (row, cell)))
            .Select(t => (index: -1, offset: t.row.LocalToGlobal(Offset.Zero), size: t.cell.Size))
            .ToList();

    static RenderObject Descend(RenderObject o)
    {
        // Row -> Padding -> RenderFlex
        RenderObject? found = null;
        void Visit(RenderObject r) { if (found is null && r is RenderFlex) found = r; else r.VisitChildren(Visit); }
        Visit(o);
        return found!;
    }

    static Widget Cell(int i) => new ColoredBox(Colors.Blue, new Text(i.ToString()));

    [Fact]
    public void FixedColumnCountSplitsTheWidthEvenly()
    {
        var h = new Harness(GridView.Builder(100, (_, i) => Cell(i), crossAxisCount: 4, childAspectRatio: 2));
        var cells = Cells(h);
        Assert.All(cells, c => Assert.Equal(100, c.size.Width, 1));
        Assert.All(cells, c => Assert.Equal(50, c.size.Height, 1));
    }

    [Fact]
    public void SpacingIsSubtractedFromTheCells()
    {
        var h = new Harness(GridView.Builder(100, (_, i) => Cell(i), crossAxisCount: 4, crossAxisSpacing: 20, mainAxisExtent: 40));
        var cells = Cells(h);
        Assert.All(cells, c => Assert.Equal(85, c.size.Width, 1));
        Assert.All(cells, c => Assert.Equal(40, c.size.Height, 1));
    }

    [Fact]
    public void MinColumnWidthFitsAsManyColumnsAsPossible()
    {
        // 400px wide, 120px minimum: three columns of 133px.
        var h = new Harness(GridView.Builder(100, (_, i) => Cell(i), minColumnWidth: 120, mainAxisExtent: 30));
        var cells = Cells(h);
        Assert.All(cells, c => Assert.Equal(400 / 3f, c.size.Width, 1));
        Assert.Equal(0, cells.Count % 3);
    }

    [Fact]
    public void OnlyVisibleRowsAreBuilt()
    {
        int built = 0;
        var h = new Harness(GridView.Builder(100000, (_, i) => { built++; return Cell(i); }, crossAxisCount: 4, mainAxisExtent: 50));
        Assert.InRange(built, 20, 120);
    }

    [Fact]
    public void ScrollsAcrossAllRows()
    {
        var controller = new ScrollController();
        var h = new Harness(GridView.Builder(10, (_, i) => Cell(i), crossAxisCount: 3, mainAxisExtent: 100, controller: controller));
        // 10 items in 3 columns is 4 rows of 100.
        Assert.Equal(100, controller.MaxScrollExtent);
    }

    [Fact]
    public void ShortLastRowKeepsItsCellsTheSameWidth()
    {
        var h = new Harness(GridView.Builder(5, (_, i) => Cell(i), crossAxisCount: 4, mainAxisExtent: 40));
        var cells = Cells(h);
        Assert.Equal(8, cells.Count);
        Assert.All(cells, c => Assert.Equal(100, c.size.Width, 1));
    }

    [Fact]
    public void RequiresExactlyOneColumnRule()
    {
        Assert.Throws<ArgumentException>(() => GridView.Builder(5, (_, i) => Cell(i)));
        Assert.Throws<ArgumentException>(() => GridView.Builder(5, (_, i) => Cell(i), crossAxisCount: 2, minColumnWidth: 100));
    }
}

using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class DataTableTests
{
    static IReadOnlyList<DataColumn> Columns(Action<int, bool>? onSort = null) =>
    [
        new DataColumn(new Text("Dessert"), Width: 140, OnSort: onSort),
        new DataColumn(new Text("Calories"), Numeric: true, Width: 100, OnSort: onSort),
    ];

    [Fact]
    public void ShowsHeadersAndCells()
    {
        var table = new DataTable(Columns(), [new DataRow([new DataCell("Frozen yogurt"), new DataCell("159")])]);
        var h = new Harness(new MaterialApp(Top(table)));
        h.Pump();
        Assert.Equal(new[] { "Dessert", "Calories", "Frozen yogurt", "159" }, Texts(h));
    }

    [Fact]
    public void HeaderTapAsksForAscendingThenDescending()
    {
        var asked = new List<(int, bool)>();
        bool ascending = true;
        int? sorted = null;
        var host = new TestHost(self => Top(new DataTable(Columns((i, asc) => { asked.Add((i, asc)); sorted = i; ascending = asc; self.Refresh!(); }), [],
            sortColumnIndex: sorted, sortAscending: ascending)));
        var h = new Harness(new MaterialApp(host));
        h.Pump();
        TapText(h, "Calories");
        TapText(h, "Calories");
        Assert.Equal(new[] { (1, true), (1, false) }, asked);
    }

    [Fact]
    public void RowCheckboxesSelectRowsAndTheHeaderSelectsAll()
    {
        var selected = new[] { false, false };
        bool? all = null;
        var host = new TestHost(self =>
        {
            DataRow Row(int i, string name) => new([new DataCell(name), new DataCell("1")], selected[i], v => { selected[i] = v; self.Refresh!(); });
            return Top(new DataTable(Columns(), [Row(0, "A"), Row(1, "B")],
                onSelectAll: v => { all = v; for (int i = 0; i < selected.Length; i++) selected[i] = v; self.Refresh!(); }));
        });
        var h = new Harness(new MaterialApp(host));
        h.Pump();

        TapText(h, "A"); // a row tap toggles its selection
        Assert.Equal(new[] { true, false }, selected);

        h.Tap(28, 28); // the header checkbox
        Assert.True(all);
        Assert.Equal(new[] { true, true }, selected);
    }

    [Fact]
    public void ScrollsSidewaysWhenColumnsAreWiderThanTheSpace()
    {
        var wide = new DataTable([new DataColumn(new Text("A"), Width: 300), new DataColumn(new Text("B"), Width: 300)],
            [new DataRow([new DataCell("x"), new DataCell("y")])]);
        var h = new Harness(new MaterialApp(Top(wide)), 400, 300);
        h.Pump();
        Assert.NotEmpty(h.Find<RenderViewport>());
    }
}

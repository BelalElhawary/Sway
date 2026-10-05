using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>
/// A Material 3 data table: a header, rows with optional selection checkboxes, and a sort arrow on the sorted column. The owner keeps
/// the sort and selection state and rebuilds the table. When the columns are wider than the space available the table scrolls sideways.
/// </summary>
public sealed class DataTable(IReadOnlyList<DataColumn> columns, IReadOnlyList<DataRow> rows, int? sortColumnIndex = null, bool sortAscending = true,
    Action<bool>? onSelectAll = null, bool showCheckboxColumn = true, float headingRowHeight = 56, float dataRowHeight = 52, Key? key = null)
    : StatelessWidget(key)
{
    const float CheckWidth = 56, CellPadding = 16;

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool selectable = showCheckboxColumn && rows.Any(r => r.OnSelectChanged is not null);
        float width = columns.Sum(c => c.Width) + (selectable ? CheckWidth : 0);

        Widget Cell(DataColumn col, Widget child, float height) => new SizedBox(width: col.Width, height: height, child: new Padding(
            EdgeInsets.Symmetric(horizontal: CellPadding), new Align(col.Numeric ? Alignment.CenterRight : Alignment.CenterLeft, child)));

        Widget Check(bool value, Action<bool>? onChanged) => new SizedBox(width: CheckWidth, child: new Center(new Checkbox(value, onChanged)));

        Widget Rule() => new SizedBox(width: width, height: 1, child: new ColoredBox(s.OutlineVariant));

        var rowsOut = new List<Widget>();

        var headerCells = new List<Widget>();
        if (selectable) headerCells.Add(Check(rows.Count > 0 && rows.All(r => r.Selected), onSelectAll));
        for (int i = 0; i < columns.Count; i++)
        {
            int index = i;
            var col = columns[i];
            bool sorted = sortColumnIndex == i;
            Widget label = DefaultTextStyle.Merge(context, theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: s.OnSurface)), col.Label);
            if (col.OnSort is null)
            {
                headerCells.Add(Cell(col, label, headingRowHeight));
                continue;
            }
            var arrow = Arrow(sorted, sortAscending, s.OnSurfaceVariant);
            Widget content = new Row(mainAxisSize: MainAxisSize.Min, spacing: 4, children: col.Numeric ? [arrow, label] : [label, arrow]);
            headerCells.Add(new Interactive((ctx, st) => new Container(
                    color: StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)),
                    child: Cell(col, content, headingRowHeight)),
                () => col.OnSort!(index, !sorted || !sortAscending)));
        }
        rowsOut.Add(new Row(headerCells));
        rowsOut.Add(Rule());

        foreach (var row in rows)
        {
            var r = row;
            var cells = new List<Widget>();
            if (selectable) cells.Add(Check(r.Selected, r.OnSelectChanged));
            for (int i = 0; i < columns.Count; i++)
            {
                Widget child = i < r.Cells.Count
                    ? DefaultTextStyle.Merge(context, theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurface)), r.Cells[i].Child)
                    : new SizedBox();
                cells.Add(Cell(columns[i], child, dataRowHeight));
            }
            Action? tap = r.OnTap ?? (r.OnSelectChanged is null ? null : () => r.OnSelectChanged(!r.Selected));
            rowsOut.Add(new Interactive((ctx, st) => new Container(
                    color: StateLayer.Blend(r.Selected ? s.SecondaryContainer.WithOpacity(0.5f) : Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)),
                    child: new Row(cells)),
                tap, focusable: tap is not null));
            rowsOut.Add(Rule());
        }

        return new LayoutBuilder((ctx, constraints) =>
        {
            Widget table = new SizedBox(width: width, child: new Column(rowsOut, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch));
            return constraints.HasBoundedWidth && width > constraints.MaxWidth
                ? new SingleChildScrollView(table, scrollDirection: Axis.Horizontal)
                : new Align(Alignment.TopLeft, table);
        });
    }

    static Widget Arrow(bool sorted, bool ascending, SKColor color) =>
        new SizedBox(18, 18, sorted ? new Icon(ascending ? Icons.ArrowUpward : Icons.ArrowDownward, 18, color) : new SizedBox());
}

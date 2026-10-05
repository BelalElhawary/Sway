using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>Carbon's row heights, in logical pixels.</summary>
public enum TableSize { ExtraSmall = 24, Small = 32, Medium = 40, Large = 48, ExtraLarge = 64 }

/// <summary>One column of a <see cref="DataTable{T}"/>.</summary>
/// <param name="Header">The header text.</param>
/// <param name="Value">The cell text for a row. Also what search matches against and what sorting falls back to.</param>
/// <param name="Width">A fixed width, or null to share the leftover width with the other flexible columns.</param>
/// <param name="Sortable">Whether clicking the header sorts by this column.</param>
/// <param name="Compare">How to order two rows; by default their <paramref name="Value"/> text is compared.</param>
/// <param name="Align">Horizontal alignment of the header and cells. Use <see cref="TextAlign.End"/> for numbers.</param>
/// <param name="Cell">A custom cell, such as a <see cref="CarbonTag"/>. Search and sorting still use <paramref name="Value"/>.</param>
/// <param name="MinWidth">The narrowest a flexible column may get. When the columns cannot all fit, the table scrolls sideways instead of squeezing them.</param>
public sealed record DataColumn<T>(string Header, Func<T, string> Value, float? Width = null, bool Sortable = true,
    Comparison<T>? Compare = null, TextAlign Align = TextAlign.Start, Func<T, Widget>? Cell = null, float MinWidth = 120)
{
    /// <summary>A column sorted by a comparable key (numbers, dates) rather than by its text.</summary>
    public static DataColumn<T> By<TKey>(string header, Func<T, TKey> key, Func<TKey, string>? format = null, float? width = null,
        TextAlign align = TextAlign.Start, Func<T, Widget>? cell = null, float minWidth = 120) where TKey : IComparable<TKey> =>
        new(header, row => format is null ? key(row)?.ToString() ?? "" : format(key(row)), width, true,
            (a, b) => Comparer<TKey>.Default.Compare(key(a), key(b)), align, cell, minWidth);
}

/// <summary>An action shown in the batch bar while rows are selected.</summary>
public sealed record BatchAction<T>(string Label, Action<IReadOnlyList<T>> OnPressed, IconData? Icon = null);

/// <summary>
/// A Carbon data table: a title, a toolbar with search, a sortable header, selectable rows and pagination.
/// Rows are virtualised, so a long page costs only what is visible. Search, sorting, selection and paging are handled
/// inside; the owner supplies the rows and listens to <c>onSelectionChanged</c> and <c>onRowTap</c>.
/// </summary>
/// <param name="maxBodyHeight">The most height the rows may take before they scroll under the header. Null shows every row of the page.</param>
/// <param name="pageSize">Rows per page, or null for no pagination.</param>
/// <param name="rowDetail">Makes rows expandable: builds the panel shown under an expanded row, which is <paramref name="detailHeight"/> tall.</param>
public sealed class DataTable<T>(IReadOnlyList<DataColumn<T>> columns, IReadOnlyList<T> rows, string? title = null, string? description = null,
    TableSize size = TableSize.Large, bool selectable = false, bool searchable = false, bool zebra = false, int? pageSize = null,
    IReadOnlyList<int>? pageSizes = null, float? maxBodyHeight = null, IReadOnlyList<Widget>? toolbarActions = null,
    IReadOnlyList<BatchAction<T>>? batchActions = null, Action<IReadOnlyList<T>>? onSelectionChanged = null, Action<T>? onRowTap = null,
    Func<T, Widget>? rowDetail = null, float detailHeight = 96, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<DataColumn<T>> Columns => columns;
    internal IReadOnlyList<T> Rows => rows;
    internal string? Title => title;
    internal string? Description => description;
    internal TableSize Size => size;
    internal bool Selectable => selectable;
    internal bool Searchable => searchable;
    internal bool Zebra => zebra;
    internal int? PageSize => pageSize;
    internal IReadOnlyList<int>? PageSizes => pageSizes;
    internal float? MaxBodyHeight => maxBodyHeight;
    internal IReadOnlyList<Widget>? ToolbarActions => toolbarActions;
    internal IReadOnlyList<BatchAction<T>>? BatchActions => batchActions;
    internal Action<IReadOnlyList<T>>? OnSelectionChanged => onSelectionChanged;
    internal Action<T>? OnRowTap => onRowTap;
    internal Func<T, Widget>? RowDetail => rowDetail;
    internal float DetailHeight => detailHeight;
    public override State CreateState() => new DataTableState<T>();
}

sealed class DataTableState<T> : State<DataTable<T>> where T : notnull
{
    const float SelectWidth = 48;
    const float ExpandWidth = 48;
    const float CellPadding = 16;

    readonly TextEditingController _search = new();
    readonly HashSet<T> _selected = new();
    readonly HashSet<T> _expanded = new();
    int _sortColumn = -1;
    bool _descending;
    int _page;
    int _pageSize;

    public override void InitState() => _pageSize = Widget.PageSize ?? int.MaxValue;

    public override void DidUpdateWidget(DataTable<T> old)
    {
        if (Widget.PageSize != old.PageSize) { _pageSize = Widget.PageSize ?? int.MaxValue; _page = 0; }
        // Rows that are gone cannot stay selected.
        if (_selected.RemoveWhere(r => !Widget.Rows.Contains(r)) > 0) NotifySelection();
        _expanded.RemoveWhere(r => !Widget.Rows.Contains(r));
    }

    // Rows after search and sort, before paging.
    List<T> View()
    {
        IEnumerable<T> rows = Widget.Rows;
        var q = _search.Text.Trim();
        if (Widget.Searchable && q.Length > 0)
            rows = rows.Where(r => Widget.Columns.Any(c => c.Value(r).Contains(q, StringComparison.CurrentCultureIgnoreCase)));
        if (_sortColumn >= 0 && _sortColumn < Widget.Columns.Count)
        {
            var col = Widget.Columns[_sortColumn];
            Comparison<T> cmp = col.Compare ?? ((a, b) => string.Compare(col.Value(a), col.Value(b), StringComparison.CurrentCultureIgnoreCase));
            var list = rows.ToList();
            // A stable sort keeps rows that compare equal in their original order.
            var ordered = _descending ? list.Select((r, i) => (r, i)).OrderByDescending(x => x.r, Comparer<T>.Create(cmp)).ThenBy(x => x.i)
                                      : list.Select((r, i) => (r, i)).OrderBy(x => x.r, Comparer<T>.Create(cmp)).ThenBy(x => x.i);
            return ordered.Select(x => x.r).ToList();
        }
        return rows.ToList();
    }

    void NotifySelection() => Widget.OnSelectionChanged?.Invoke(Widget.Rows.Where(_selected.Contains).ToList());

    void Sort(int column)
    {
        // none -> ascending -> descending -> none
        SetState(() =>
        {
            if (_sortColumn != column) { _sortColumn = column; _descending = false; }
            else if (!_descending) _descending = true;
            else _sortColumn = -1;
            _page = 0;
        });
    }

    void ToggleRow(T row, bool on)
    {
        SetState(() => { if (on) _selected.Add(row); else _selected.Remove(row); });
        NotifySelection();
    }

    void ToggleAll(List<T> view, bool on)
    {
        SetState(() => { if (on) foreach (var r in view) _selected.Add(r); else foreach (var r in view) _selected.Remove(r); });
        NotifySelection();
    }

    void ClearSelection()
    {
        SetState(_selected.Clear);
        NotifySelection();
    }

    bool Expandable => Widget.RowDetail is not null;

    // The fixed-width columns before the data: the expand chevron and the selection checkbox.
    float LeadingWidth => (Expandable ? ExpandWidth : 0) + (Widget.Selectable ? SelectWidth : 0);

    // The width the columns need before the table has to scroll sideways.
    float MinContentWidth => LeadingWidth + Widget.Columns.Sum(c => c.Width ?? c.MinWidth);

    Widget Cell(DataColumn<T> col, Widget child)
    {
        Widget cell = new Padding(EdgeInsets.Symmetric(horizontal: CellPadding), new Align(col.Align == TextAlign.End ? Alignment.CenterRight : col.Align == TextAlign.Center ? Alignment.Center : AlignmentDirectional.CenterStart, child));
        return col.Width is { } w ? new SizedBox(width: w, child: cell) : new Expanded(cell);
    }

    Widget HeaderCell(int index, DataColumn<T> col, CarbonThemeData theme, SKColor back)
    {
        var k = theme.Colors;
        var style = theme.Type.HeadingCompact01.Merge(new TextStyle(Color: k.TextPrimary));
        bool sorted = _sortColumn == index;

        Widget Label(bool hover)
        {
            var icon = sorted ? (_descending ? Icons.ArrowDownward : Icons.ArrowUpward) : hover && col.Sortable ? Icons.UnfoldMore : null;
            return new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new Flexible(new Text(col.Header, style: style, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, textAlign: col.Align)),
                new SizedBox(width: 16, height: 16, child: icon is null ? null : new Icon(icon, 16, k.IconPrimary)),
            ]);
        }

        // The cell already sizes itself (fixed width or Expanded), so the header cell is a Row holding it.
        Widget header = col.Sortable
            ? new Interactive((ctx, st) => new Container(
                color: st.Pressed ? k.LayerAccentHover01 : st.Hover || sorted ? k.LayerAccentHover01 : back,
                child: CarbonFocus.Around(st.FocusVisible, theme, new Row(children: [Cell(col, Label(st.Hover))]))), () => Sort(index))
            : new Row(children: [Cell(col, Label(false))]);
        return col.Width is { } w ? new SizedBox(width: w, child: header) : new Expanded(header);
    }

    Widget Toolbar(CarbonThemeData theme, float height)
    {
        var k = theme.Colors;
        var picked = Widget.Rows.Where(_selected.Contains).ToList();
        if (picked.Count > 0)
        {
            // While rows are selected the toolbar becomes the batch bar.
            return new Container(height: height, color: k.ButtonPrimary, padding: EdgeInsets.Only(left: 16), child: new Row(
                crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Text($"{picked.Count} item{(picked.Count == 1 ? "" : "s")} selected",
                    style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextOnColor))),
                new Expanded(new SizedBox()),
                ..(Widget.BatchActions ?? []).Select(a => (Widget)new CarbonButton(new Text(a.Label), () => a.OnPressed(picked),
                    CarbonButtonKind.GhostOnColor, CarbonButtonSize.Large, a.Icon)),
                new CarbonButton(new Text("Cancel"), ClearSelection, CarbonButtonKind.GhostOnColor, CarbonButtonSize.Large),
            ]));
        }

        Widget search = Widget.Searchable
            ? new Expanded(new Align(AlignmentDirectional.CenterStart, new ConstrainedBox(new BoxConstraints(0, 320, 0, float.PositiveInfinity),
                new CarbonSearch(_search, placeholder: "Search", size: CarbonFieldSize.Large, onLayer: true, onChanged: _ => SetState(() => _page = 0)))))
            : new Expanded(new SizedBox());
        return new Container(height: height, color: k.Layer01, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            search,
            ..Widget.ToolbarActions ?? [],
        ]));
    }

    Widget BuildRow(BuildContext context, int index, T row, float rowHeight, CarbonThemeData theme)
    {
        var k = theme.Colors;
        bool picked = _selected.Contains(row);
        bool tappable = Widget.OnRowTap is not null;
        var back = picked ? k.LayerSelected01 : Widget.Zebra && index % 2 == 1 ? k.Layer02 : k.Layer01;
        var hover = picked ? k.LayerSelectedHover01 : Widget.Zebra && index % 2 == 1 ? k.LayerHover02 : k.LayerHover01;
        var style = theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextPrimary));

        Widget line = new Interactive((ctx, st) => new Container(
            color: st.Hover || st.Pressed ? hover : back,
            child: new Column(children:
            [
                new SizedBox(height: rowHeight - 1, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    ..Expandable
                        ? [new SizedBox(width: ExpandWidth, height: rowHeight - 1, child: new GestureDetector(
                            onTap: () => SetState(() => { if (!_expanded.Remove(row)) _expanded.Add(row); }), behavior: HitTestBehavior.Opaque,
                            child: new Center(new Icon(_expanded.Contains(row) ? Icons.ExpandLess : Icons.ExpandMore, 16, k.IconPrimary))))]
                        : Array.Empty<Widget>(),
                    ..Widget.Selectable
                        ? [new SizedBox(width: SelectWidth, height: rowHeight - 1, child: new Center(new CarbonCheckbox(picked, v => ToggleRow(row, v), hitPadding: 8)))]
                        : Array.Empty<Widget>(),
                    ..Widget.Columns.Select(col => Cell(col, col.Cell?.Invoke(row)
                        ?? new Text(col.Value(row), style: style, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, textAlign: col.Align))),
                ])),
                new Container(height: 1, color: k.BorderSubtle01),
            ])),
            tappable ? () => Widget.OnRowTap!(row) : () => { }, cursor: tappable ? MouseCursor.Click : MouseCursor.Default, focusable: false);

        if (!Expandable || !_expanded.Contains(row)) return line;
        // The detail sits outside the hover layer so only the row itself lights up.
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            line,
            new Container(height: Widget.DetailHeight, color: k.Layer02, padding: EdgeInsets.All(16), child: Widget.RowDetail!(row)),
            new Container(height: 1, color: k.BorderSubtle01),
        ]);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var k = theme.Colors;
        float rowHeight = (float)Widget.Size;
        const float barHeight = 48;

        var view = View();
        int pages = Pagination.PageCount(view.Count, _pageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var visible = Widget.PageSize is null ? view : view.Skip(_page * _pageSize).Take(_pageSize).ToList();

        int openRows = Expandable ? visible.Count(_expanded.Contains) : 0;
        float contentHeight = visible.Count * rowHeight + openRows * (Widget.DetailHeight + 1);
        float bodyHeight = Widget.MaxBodyHeight is { } max ? Math.Min(contentHeight, max) : contentHeight;
        bool allPicked = visible.Count > 0 && visible.All(_selected.Contains);
        bool somePicked = !allPicked && visible.Any(_selected.Contains);
        var headBack = k.LayerAccent01;

        Widget body = visible.Count == 0
            ? new Container(height: rowHeight * 2, color: k.Layer01, alignment: Alignment.Center,
                child: new Text(_search.Text.Trim().Length > 0 ? "No matching results" : "No data",
                    style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextSecondary))))
            : new SizedBox(height: bodyHeight, child: ListView.Builder(visible.Count, (ctx, i) => BuildRow(ctx, i, visible[i], rowHeight, theme),
                // Expanded rows are taller, so the extent is only fixed while nothing is open.
                openRows == 0 ? rowHeight : null));

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
        [
            ..Widget.Title is null && Widget.Description is null ? Array.Empty<Widget>() :
            new Widget[]
            {
                new Container(color: k.Layer01, padding: EdgeInsets.Only(left: 16, right: 16, top: 16, bottom: 16), child: new Column(
                    crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    ..Widget.Title is null ? Array.Empty<Widget>() : [new Text(Widget.Title, style: theme.Type.Heading03.Merge(new TextStyle(Color: k.TextPrimary)))],
                    ..Widget.Description is null ? Array.Empty<Widget>() : [new Text(Widget.Description, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextSecondary)))],
                ])),
            },
            ..Widget.Searchable || Widget.ToolbarActions is { Count: > 0 } || _selected.Count > 0 ? [Toolbar(theme, barHeight)] : Array.Empty<Widget>(),
            new LayoutBuilder((ctx, box) =>
            {
                Widget grid = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Container(color: headBack, height: rowHeight, child: new Row(children:
                    [
                        ..Expandable ? [new SizedBox(width: ExpandWidth)] : Array.Empty<Widget>(),
                        ..Widget.Selectable
                            ? [new SizedBox(width: SelectWidth, height: rowHeight, child: new Center(new CarbonCheckbox(allPicked, v => ToggleAll(visible, v), indeterminate: somePicked, hitPadding: 8)))]
                            : Array.Empty<Widget>(),
                        ..Widget.Columns.Select((col, i) => HeaderCell(i, col, theme, headBack)),
                    ])),
                    body,
                ]);
                // When the columns do not fit, the header and rows scroll sideways together; the toolbar and pager stay put.
                return box.MaxWidth < MinContentWidth
                    ? new SingleChildScrollView(new SizedBox(width: MinContentWidth, child: grid), Axis.Horizontal, wheelScrollsOtherAxis: false)
                    : grid;
            }),
            ..Widget.PageSize is null ? Array.Empty<Widget>() :
            [
                new Pagination(view.Count, _page, _pageSize, p => SetState(() => _page = p),
                    n => SetState(() => { _pageSize = n; _page = 0; }), Widget.PageSizes),
            ],
        ]);
    }
}

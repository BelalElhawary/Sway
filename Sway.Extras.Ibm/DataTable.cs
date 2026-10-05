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
/// <param name="Cell">A custom cell, such as a <see cref="Tag"/>. Search and sorting still use <paramref name="Value"/>.</param>
public sealed record DataColumn<T>(string Header, Func<T, string> Value, float? Width = null, bool Sortable = true,
    Comparison<T>? Compare = null, TextAlign Align = TextAlign.Start, Func<T, Widget>? Cell = null)
{
    /// <summary>A column sorted by a comparable key (numbers, dates) rather than by its text.</summary>
    public static DataColumn<T> By<TKey>(string header, Func<T, TKey> key, Func<TKey, string>? format = null, float? width = null,
        TextAlign align = TextAlign.Start, Func<T, Widget>? cell = null) where TKey : IComparable<TKey> =>
        new(header, row => format is null ? key(row)?.ToString() ?? "" : format(key(row)), width, true,
            (a, b) => Comparer<TKey>.Default.Compare(key(a), key(b)), align, cell);
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
public sealed class DataTable<T>(IReadOnlyList<DataColumn<T>> columns, IReadOnlyList<T> rows, string? title = null, string? description = null,
    TableSize size = TableSize.Large, bool selectable = false, bool searchable = false, bool zebra = false, int? pageSize = null,
    IReadOnlyList<int>? pageSizes = null, float? maxBodyHeight = null, IReadOnlyList<Widget>? toolbarActions = null,
    IReadOnlyList<BatchAction<T>>? batchActions = null, Action<IReadOnlyList<T>>? onSelectionChanged = null, Action<T>? onRowTap = null,
    Key? key = null) : StatefulWidget(key) where T : notnull
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
    public override State CreateState() => new DataTableState<T>();
}

sealed class DataTableState<T> : State<DataTable<T>> where T : notnull
{
    const float SelectWidth = 48;
    const float CellPadding = 16;

    readonly TextEditingController _search = new();
    readonly HashSet<T> _selected = new();
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

    Widget Cell(DataColumn<T> col, Widget child)
    {
        Widget cell = new Padding(EdgeInsets.Symmetric(horizontal: CellPadding), new Align(col.Align == TextAlign.End ? Alignment.CenterRight : col.Align == TextAlign.Center ? Alignment.Center : AlignmentDirectional.CenterStart, child));
        return col.Width is { } w ? new SizedBox(width: w, child: cell) : new Expanded(cell);
    }

    Widget HeaderCell(int index, DataColumn<T> col, ThemeData theme, SKColor back)
    {
        var s = theme.ColorScheme;
        var style = theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurface, FontWeight: FontWeight.W600));
        bool sorted = _sortColumn == index;

        Widget Label(bool hover)
        {
            var icon = sorted ? (_descending ? Icons.ArrowDownward : Icons.ArrowUpward) : hover && col.Sortable ? Icons.UnfoldMore : null;
            return new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new Flexible(new Text(col.Header, style: style, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, textAlign: col.Align)),
                new SizedBox(width: 16, height: 16, child: icon is null ? null : new Icon(icon, 16, s.OnSurface)),
            ]);
        }

        // The cell already sizes itself (fixed width or Expanded), so the header cell is a Row holding it.
        Widget header = col.Sortable
            ? new Interactive((ctx, st) => new Container(
                color: StateLayer.Blend(sorted ? s.OutlineVariant : back, s.OnSurface, StateLayer.Opacity(st)),
                child: new Row(children: [Cell(col, Label(st.Hover))])), () => Sort(index))
            : new Row(children: [Cell(col, Label(false))]);
        return col.Width is { } w ? new SizedBox(width: w, child: header) : new Expanded(header);
    }

    Widget Toolbar(ThemeData theme, float height)
    {
        var s = theme.ColorScheme;
        var picked = Widget.Rows.Where(_selected.Contains).ToList();
        if (picked.Count > 0)
        {
            // While rows are selected the toolbar becomes the batch bar.
            return new Container(height: height, color: s.Primary, padding: EdgeInsets.Symmetric(horizontal: 16), child: new Row(
                crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
            [
                new Text($"{picked.Count} item{(picked.Count == 1 ? "" : "s")} selected",
                    style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnPrimary))),
                new Expanded(new SizedBox()),
                ..(Widget.BatchActions ?? []).Select(a => (Widget)new TextButton(new Text(a.Label), () => a.OnPressed(picked), a.Icon, s.OnPrimary)),
                new TextButton(new Text("Cancel"), ClearSelection, color: s.OnPrimary),
            ]));
        }

        Widget search = Widget.Searchable
            ? new Expanded(new Align(AlignmentDirectional.CenterStart, new ConstrainedBox(new BoxConstraints(0, 320, 0, float.PositiveInfinity),
                new TextField(_search, decoration: new InputDecoration(HintText: "Search", Prefix: new Icon(Icons.Search, 20), Filled: true),
                    onChanged: _ => SetState(() => _page = 0)))))
            : new Expanded(new SizedBox());
        return new Container(height: height, color: s.SurfaceContainerHigh, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
        [
            search,
            ..Widget.ToolbarActions ?? [],
            new SizedBox(width: 8),
        ]));
    }

    Widget BuildRow(BuildContext context, int index, T row, float rowHeight, ThemeData theme)
    {
        var s = theme.ColorScheme;
        bool picked = _selected.Contains(row);
        bool tappable = Widget.OnRowTap is not null;
        var back = picked ? Lerps.Color(s.SurfaceContainerLow, s.Primary, 0.12f)
            : Widget.Zebra && index % 2 == 1 ? s.SurfaceContainerHigh : s.SurfaceContainerLow;
        var style = theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurface));

        return new Interactive((ctx, st) => new Container(
            color: StateLayer.Blend(back, s.OnSurface, StateLayer.Opacity(st) * (tappable ? 1 : 0.6f)),
            child: new Column(children:
            [
                new SizedBox(height: rowHeight - 1, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    ..Widget.Selectable
                        ? [new SizedBox(width: SelectWidth, height: rowHeight - 1, child: new OverflowBox(new Checkbox(picked, v => ToggleRow(row, v))))]
                        : Array.Empty<Widget>(),
                    ..Widget.Columns.Select(col => Cell(col, col.Cell?.Invoke(row)
                        ?? new Text(col.Value(row), style: style, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, textAlign: col.Align))),
                ])),
                new Container(height: 1, color: s.OutlineVariant),
            ])),
            tappable ? () => Widget.OnRowTap!(row) : () => { }, cursor: tappable ? MouseCursor.Click : MouseCursor.Default, focusable: false);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        float rowHeight = (float)Widget.Size;
        float barHeight = Math.Max(48, theme.Shape.FieldHeight);

        var view = View();
        int pages = Pagination.PageCount(view.Count, _pageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var visible = Widget.PageSize is null ? view : view.Skip(_page * _pageSize).Take(_pageSize).ToList();

        float contentHeight = visible.Count * rowHeight;
        float bodyHeight = Widget.MaxBodyHeight is { } max ? Math.Min(contentHeight, max) : contentHeight;
        bool allPicked = visible.Count > 0 && visible.All(_selected.Contains);
        var headBack = s.SecondaryContainer;

        Widget body = visible.Count == 0
            ? new Container(height: rowHeight * 2, color: s.SurfaceContainerLow, alignment: Alignment.Center,
                child: new Text(_search.Text.Trim().Length > 0 ? "No matching results" : "No data",
                    style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant))))
            : new SizedBox(height: bodyHeight, child: ListView.Builder(visible.Count, (ctx, i) => BuildRow(ctx, i, visible[i], rowHeight, theme), rowHeight));

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
        [
            ..Widget.Title is null && Widget.Description is null ? Array.Empty<Widget>() :
            new Widget[]
            {
                new Container(color: s.SurfaceContainerLow, padding: EdgeInsets.Only(left: 16, right: 16, top: 16, bottom: 16), child: new Column(
                    crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    ..Widget.Title is null ? Array.Empty<Widget>() : [new Text(Widget.Title, style: theme.TextTheme.TitleLarge.Merge(new TextStyle(Color: s.OnSurface)))],
                    ..Widget.Description is null ? Array.Empty<Widget>() : [new Text(Widget.Description, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)))],
                ])),
            },
            ..Widget.Searchable || Widget.ToolbarActions is { Count: > 0 } || _selected.Count > 0 ? [Toolbar(theme, barHeight)] : Array.Empty<Widget>(),
            new Container(color: headBack, height: rowHeight, child: new Row(children:
            [
                ..Widget.Selectable
                    ? [new SizedBox(width: SelectWidth, height: rowHeight, child: new OverflowBox(new Checkbox(allPicked, v => ToggleAll(visible, v))))]
                    : Array.Empty<Widget>(),
                ..Widget.Columns.Select((c, i) => HeaderCell(i, c, theme, headBack)),
            ])),
            body,
            ..Widget.PageSize is null ? Array.Empty<Widget>() :
            [
                new Pagination(view.Count, _page, _pageSize, p => SetState(() => _page = p),
                    n => SetState(() => { _pageSize = n; _page = 0; }), Widget.PageSizes),
            ],
        ]);
    }
}

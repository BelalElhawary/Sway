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
/// <param name="OnEdit">Makes the column editable: double-clicking a cell edits its <paramref name="Value"/> text, and Enter (or leaving the cell) calls this with the row and the new text.
/// The table never changes the rows itself, so the owner applies the edit and passes the updated rows back. Escape cancels.</param>
public sealed record DataColumn<T>(string Header, Func<T, string> Value, float? Width = null, bool Sortable = true,
    Comparison<T>? Compare = null, TextAlign Align = TextAlign.Start, Func<T, Widget>? Cell = null, float MinWidth = 120,
    Action<T, string>? OnEdit = null)
{
    /// <summary>A column sorted by a comparable key (numbers, dates) rather than by its text.</summary>
    public static DataColumn<T> By<TKey>(string header, Func<T, TKey> key, Func<TKey, string>? format = null, float? width = null,
        TextAlign align = TextAlign.Start, Func<T, Widget>? cell = null, float minWidth = 120, Action<T, string>? onEdit = null) where TKey : IComparable<TKey> =>
        new(header, row => format is null ? key(row)?.ToString() ?? "" : format(key(row)), width, true,
            (a, b) => Comparer<TKey>.Default.Compare(key(a), key(b)), align, cell, minWidth, onEdit);
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
/// <param name="rowDetail">Makes rows expandable: builds the panel shown under an expanded row.</param>
/// <param name="detailHeight">A fixed height for the panel, or null to size it to its content. Content-sized panels make the body a plain scroller instead of a virtualised list while any row is open.</param>
/// <param name="resizable">Lets the user drag the right edge of a header to resize its column (never below 48).</param>
/// <param name="stickyColumns">How many leading columns stay in view (after the expand and selection columns) while the rest scroll sideways.</param>
public sealed class DataTable<T>(IReadOnlyList<DataColumn<T>> columns, IReadOnlyList<T> rows, string? title = null, string? description = null,
    TableSize size = TableSize.Large, bool selectable = false, bool searchable = false, bool zebra = false, int? pageSize = null,
    IReadOnlyList<int>? pageSizes = null, float? maxBodyHeight = null, IReadOnlyList<Widget>? toolbarActions = null,
    IReadOnlyList<BatchAction<T>>? batchActions = null, Action<IReadOnlyList<T>>? onSelectionChanged = null, Action<T>? onRowTap = null,
    Func<T, Widget>? rowDetail = null, float? detailHeight = null, bool resizable = false, int stickyColumns = 0, Key? key = null) : StatefulWidget(key) where T : notnull
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
    internal float? DetailHeight => detailHeight;
    internal bool Resizable => resizable;
    internal int StickyColumns => stickyColumns;
    public override State CreateState() => new DataTableState<T>();
}

sealed class DataTableState<T> : State<DataTable<T>> where T : notnull
{
    const float SelectWidth = 48;
    const float ExpandWidth = 48;
    const float CellPadding = 16;
    const float MinResizedWidth = 48;
    const float ResizeHandleWidth = 8;

    readonly TextEditingController _search = new();
    readonly HashSet<T> _selected = new();
    readonly HashSet<T> _expanded = new();
    int _sortColumn = -1;
    bool _descending;
    int _page;
    int _pageSize;

    // Widths the user dragged columns to, by column index.
    readonly Dictionary<int, float> _widths = new();
    float _dragStartWidth, _dragTravel;
    // One position shared by every row's sideways viewport when columns are pinned.
    readonly ScrollController _sideways = new();

    // The cell being edited, and the field that edits it.
    (T Row, int Column)? _editing;
    bool _editFocused;
    readonly TextEditingController _editor = new();
    readonly FocusNode _editNode = new() { DebugLabel = "DataTable cell editor" };

    public override void InitState() => _pageSize = Widget.PageSize ?? int.MaxValue;

    // The editor's own key handling belongs to the text field, so Escape and losing focus are watched from a Focus around it.
    bool EditorKey(KeyEvent e)
    {
        if (!e.IsDown || e.Key != "Escape" || _editing is null) return false;
        CancelEdit();
        return true;
    }

    void EditorFocusChanged(bool focused)
    {
        if (_editing is null) return;
        if (focused) _editFocused = true;
        else if (_editFocused) CommitEdit();
    }

    void StartEdit(T row, int column)
    {
        if (_editing is { } cur && !(EqualityComparer<T>.Default.Equals(cur.Row, row) && cur.Column == column)) CommitEdit();
        _editor.Text = Widget.Columns[column].Value(row);
        _editFocused = false;
        SetState(() => _editing = (row, column));
        WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted && _editing is not null) _editNode.RequestFocus(); });
    }

    void CommitEdit()
    {
        if (_editing is not var (row, column)) return;
        _editing = null;
        _editFocused = false;
        var text = _editor.Text;
        SetState();
        if (text != Widget.Columns[column].Value(row)) Widget.Columns[column].OnEdit?.Invoke(row, text);
    }

    void CancelEdit()
    {
        if (_editing is null) return;
        _editing = null;
        _editFocused = false;
        SetState();
        _editNode.Unfocus();
    }

    public override void DidUpdateWidget(DataTable<T> old)
    {
        if (Widget.PageSize != old.PageSize) { _pageSize = Widget.PageSize ?? int.MaxValue; _page = 0; }
        // Rows that are gone cannot stay selected.
        if (_selected.RemoveWhere(r => !Widget.Rows.Contains(r)) > 0) NotifySelection();
        _expanded.RemoveWhere(r => !Widget.Rows.Contains(r));
        if (_editing is { } e && (!Widget.Rows.Contains(e.Row) || e.Column >= Widget.Columns.Count)) { _editing = null; _editFocused = false; }
        foreach (var i in _widths.Keys.Where(i => i >= Widget.Columns.Count).ToList()) _widths.Remove(i);
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

    // Every column's width for the given table width: a dragged or fixed width as is, flexible ones sharing what is left
    // but never narrower than their MinWidth. The total can exceed the table width, which is when it scrolls sideways.
    float[] ColumnWidths(float available)
    {
        var cols = Widget.Columns;
        var widths = new float[cols.Count];
        float fixedSum = 0;
        int flexible = 0;
        for (int i = 0; i < cols.Count; i++)
        {
            if (_widths.TryGetValue(i, out var dragged)) widths[i] = dragged;
            else if (cols[i].Width is { } w) widths[i] = w;
            else { flexible++; continue; }
            fixedSum += widths[i];
        }
        float share = flexible > 0 ? Math.Max(0, available - LeadingWidth - fixedSum) / flexible : 0;
        for (int i = 0; i < cols.Count; i++)
            if (!_widths.ContainsKey(i) && cols[i].Width is null) widths[i] = Math.Max(cols[i].MinWidth, share);
        return widths;
    }

    Widget Cell(DataColumn<T> col, float width, Widget child) =>
        new SizedBox(width: width, child: new Padding(EdgeInsets.Symmetric(horizontal: CellPadding),
            new Align(col.Align == TextAlign.End ? Alignment.CenterRight : col.Align == TextAlign.Center ? Alignment.Center : AlignmentDirectional.CenterStart, child)));

    // Lays out one line of the table. With pinned columns the leading widgets and the first columns stay put and the rest
    // sit in a viewport driven by the shared sideways position; otherwise it is a plain row.
    Widget Line(List<Widget> leading, List<Widget> cells, float[] widths, int pinned)
    {
        if (pinned <= 0) return new Row(crossAxisAlignment: CrossAxisAlignment.Center, children: [.. leading, .. cells]);
        float pinnedWidth = LeadingWidth + widths.Take(pinned).Sum(), scrolling = widths.Skip(pinned).Sum();
        return new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new SizedBox(width: pinnedWidth, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children: [.. leading, .. cells.Take(pinned)])),
            new Expanded(new Viewport(Axis.Horizontal, _sideways.Position, new SizedBox(width: scrolling,
                child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children: [.. cells.Skip(pinned)])))),
        ]);
    }

    Widget Editor(float width, float height, CarbonThemeData theme)
    {
        var k = theme.Colors;
        var style = theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextPrimary));
        return new SizedBox(width: width, height: height, child: new Focus(onKey: EditorKey, onFocusChange: EditorFocusChanged,
            child: CarbonFocus.Around(true, theme, new Container(color: k.Field01,
                padding: EdgeInsets.Symmetric(horizontal: CellPadding - 2), alignment: AlignmentDirectional.CenterStart,
                child: new EditableText(_editor, _editNode, style, style.Merge(new TextStyle(Color: k.TextPlaceholder)), null, false, 1, null, false,
                    k.TextPrimary, k.Highlight, null, _ => CommitEdit())))));
    }

    Widget HeaderCell(int index, DataColumn<T> col, float width, CarbonThemeData theme, SKColor back)
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

        // The cell already sizes itself, so the header cell is a Row holding it.
        Widget header = col.Sortable
            ? new Interactive((ctx, st) => new Container(
                color: st.Pressed ? k.LayerAccentHover01 : st.Hover || sorted ? k.LayerAccentHover01 : back,
                child: CarbonFocus.Around(st.FocusVisible, theme, new Row(children: [Cell(col, width, Label(st.Hover))]))), () => Sort(index))
            : new Row(children: [Cell(col, width, Label(false))]);
        if (!Widget.Resizable) return header;

        // The handle sits over the right edge of the header and is the only part that drags.
        var handle = new MouseRegion(cursor: MouseCursor.ResizeHorizontal, child: new GestureDetector(behavior: HitTestBehavior.Opaque,
            onHorizontalDragStart: _ => { _dragStartWidth = width; _dragTravel = 0; },
            onHorizontalDragUpdate: d =>
            {
                _dragTravel += d.Delta.Dx;
                SetState(() => _widths[index] = Math.Max(MinResizedWidth, _dragStartWidth + _dragTravel));
            },
            child: new Align(Alignment.CenterRight, new Container(width: 1, height: 16, color: k.BorderStrong01))));
        return new SizedBox(width: width, child: new Stack([header, new Positioned(handle, top: 0, bottom: 0, right: 0, width: ResizeHandleWidth)], clip: false));
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

    Widget BodyCell(T row, int column, DataColumn<T> col, float width, float height, CarbonThemeData theme, TextStyle style)
    {
        if (_editing is { } e && e.Column == column && EqualityComparer<T>.Default.Equals(e.Row, row)) return Editor(width, height, theme);
        Widget cell = Cell(col, width, col.Cell?.Invoke(row)
            ?? new Text(col.Value(row), style: style, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, textAlign: col.Align));
        return col.OnEdit is null ? cell : new GestureDetector(cell, onDoubleTap: () => StartEdit(row, column), behavior: HitTestBehavior.Opaque);
    }

    Widget BuildRow(BuildContext context, int index, T row, float rowHeight, CarbonThemeData theme, float[] widths, int pinned)
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
                new SizedBox(height: rowHeight - 1, child: Line(
                [
                    ..Expandable
                        ? [new SizedBox(width: ExpandWidth, height: rowHeight - 1, child: new GestureDetector(
                            onTap: () => SetState(() => { if (!_expanded.Remove(row)) _expanded.Add(row); }), behavior: HitTestBehavior.Opaque,
                            child: new Center(new Icon(_expanded.Contains(row) ? Icons.ExpandLess : Icons.ExpandMore, 16, k.IconPrimary))))]
                        : Array.Empty<Widget>(),
                    ..Widget.Selectable
                        ? [new SizedBox(width: SelectWidth, height: rowHeight - 1, child: new Center(new CarbonCheckbox(picked, v => ToggleRow(row, v), hitPadding: 8)))]
                        : Array.Empty<Widget>(),
                ], Widget.Columns.Select((col, c) => BodyCell(row, c, col, widths[c], rowHeight - 1, theme, style)).ToList(), widths, pinned)),
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
        // Panels sized to their content have no known height, so the rows cannot be virtualised into a box of known height.
        bool measured = openRows > 0 && Widget.DetailHeight is null;
        float contentHeight = visible.Count * rowHeight + openRows * ((Widget.DetailHeight ?? 0) + 1);
        float bodyHeight = Widget.MaxBodyHeight is { } max ? Math.Min(contentHeight, max) : contentHeight;
        bool allPicked = visible.Count > 0 && visible.All(_selected.Contains);
        bool somePicked = !allPicked && visible.Any(_selected.Contains);
        var headBack = k.LayerAccent01;

        Widget Body(float[] widths, int pinned)
        {
            if (visible.Count == 0)
                return new Container(height: rowHeight * 2, color: k.Layer01, alignment: Alignment.Center,
                    child: new Text(_search.Text.Trim().Length > 0 ? "No matching results" : "No data",
                        style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: k.TextSecondary))));
            if (measured)
            {
                Widget rows = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min,
                    children: visible.Select((r, i) => BuildRow(context, i, r, rowHeight, theme, widths, pinned)).ToList());
                return Widget.MaxBodyHeight is { } cap
                    ? new ConstrainedBox(new BoxConstraints(0, float.PositiveInfinity, 0, cap), new SingleChildScrollView(rows, shrinkWrap: true))
                    : rows;
            }
            return new SizedBox(height: bodyHeight, child: ListView.Builder(visible.Count, (ctx, i) => BuildRow(ctx, i, visible[i], rowHeight, theme, widths, pinned),
                // Expanded rows are taller, so the extent is only fixed while nothing is open.
                openRows == 0 ? rowHeight : null));
        }

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
                var widths = ColumnWidths(box.MaxWidth);
                float total = LeadingWidth + widths.Sum();
                bool scrolls = total > box.MaxWidth + 0.5f;
                // Pinned columns need room left over for the ones that scroll, or they would just hide them.
                int pinned = Math.Clamp(Widget.StickyColumns, 0, widths.Length);
                if (!scrolls || LeadingWidth + widths.Take(pinned).Sum() > box.MaxWidth - 120) pinned = 0;
                else if (pinned == widths.Length) pinned = 0;

                Widget grid = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Container(color: headBack, height: rowHeight, child: Line(
                    [
                        ..Expandable ? [new SizedBox(width: ExpandWidth)] : Array.Empty<Widget>(),
                        ..Widget.Selectable
                            ? [new SizedBox(width: SelectWidth, height: rowHeight, child: new Center(new CarbonCheckbox(allPicked, v => ToggleAll(visible, v), indeterminate: somePicked, hitPadding: 8)))]
                            : Array.Empty<Widget>(),
                    ], widths.Select((w, i) => HeaderCell(i, Widget.Columns[i], w, theme, headBack)).ToList(), widths, pinned)),
                    Body(widths, pinned),
                ]);
                // When the columns do not fit, the header and rows scroll sideways together; the toolbar and pager stay put.
                // Pinned columns are laid out inside each row instead, all sharing one position that this scrollable drives.
                if (!scrolls) return grid;
                return pinned > 0
                    ? new Scrollable(Axis.Horizontal, (_, _) => grid, _sideways, wheelScrollsOtherAxis: false)
                    : new SingleChildScrollView(new SizedBox(width: total, child: grid), Axis.Horizontal, wheelScrollsOtherAxis: false);
            }),
            ..Widget.PageSize is null ? Array.Empty<Widget>() :
            [
                new Pagination(view.Count, _page, _pageSize, p => SetState(() => _page = p),
                    n => SetState(() => { _pageSize = n; _page = 0; }), Widget.PageSizes),
            ],
        ]);
    }
}

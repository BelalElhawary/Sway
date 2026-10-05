using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

public sealed record CarbonBreadcrumbItem(string Label, Action? OnPressed = null);

/// <summary>A Carbon breadcrumb: links separated by slashes. The last item is the current page and is not a link.</summary>
public sealed class CarbonBreadcrumb(IReadOnlyList<CarbonBreadcrumbItem> items, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var parts = new List<Widget>();
        for (int i = 0; i < items.Count; i++)
        {
            bool last = i == items.Count - 1;
            if (i > 0) parts.Add(new Text("/", style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary))));
            parts.Add(last ? new Text(items[i].Label, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary)))
                : new CarbonLink(items[i].Label, items[i].OnPressed ?? (() => { })));
        }
        return new Wrap(spacing: 8, runSpacing: 4, children: parts);
    }
}

public sealed record CarbonAccordionItem(string Title, Widget Content, bool Disabled = false);

/// <summary>A Carbon accordion: headings that each reveal a panel. <paramref name="allowMultiple"/> keeps several open at once.</summary>
public sealed class CarbonAccordion(IReadOnlyList<CarbonAccordionItem> items, bool allowMultiple = false, IReadOnlyCollection<int>? initiallyOpen = null, Key? key = null)
    : StatefulWidget(key)
{
    internal IReadOnlyList<CarbonAccordionItem> Items => items;
    internal bool AllowMultiple => allowMultiple;
    internal IReadOnlyCollection<int>? InitiallyOpen => initiallyOpen;
    public override State CreateState() => new CarbonAccordionState();
}

sealed class CarbonAccordionState : State<CarbonAccordion>
{
    readonly HashSet<int> _open = new();

    public override void InitState()
    {
        foreach (var i in Widget.InitiallyOpen ?? []) _open.Add(i);
    }

    void Toggle(int i) => SetState(() =>
    {
        if (_open.Remove(i)) return;
        if (!Widget.AllowMultiple) _open.Clear();
        _open.Add(i);
    });

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var rows = new List<Widget>();
        for (int n = 0; n < Widget.Items.Count; n++)
        {
            int i = n;
            var item = Widget.Items[n];
            bool open = _open.Contains(i);
            rows.Add(new Container(height: 1, color: c.BorderSubtle01));
            rows.Add(new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(height: 40,
                color: st.Hover && !item.Disabled ? c.BackgroundHover : Colors.Transparent, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    new SizedBox(width: 40, child: new Center(new Icon(open ? Icons.KeyboardArrowDown : Icons.ChevronRight, 16, item.Disabled ? c.IconDisabled : c.IconPrimary))),
                    new Expanded(new Text(item.Title, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                        style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: item.Disabled ? c.TextDisabled : c.TextPrimary)))),
                ]))), item.Disabled ? null : () => Toggle(i), focusable: !item.Disabled));
            if (open) rows.Add(new Padding(EdgeInsets.Only(left: 40, right: 16, top: 8, bottom: 24),
                DefaultTextStyle.Merge(context, theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary)), item.Content)));
        }
        rows.Add(new Container(height: 1, color: c.BorderSubtle01));
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: rows);
    }
}

/// <summary>A Carbon tile: a plain 1px-padded container on the layer background. <paramref name="onLayer"/> picks the next layer up, for use inside another tile or a layered page.</summary>
public sealed class CarbonTile(Widget child, bool onLayer = false, float? width = null, float? height = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        return new Container(width: width, height: height, padding: EdgeInsets.All(16), color: onLayer ? c.Layer02 : c.Layer01,
            child: DefaultTextStyle.Merge(context, theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary)), child));
    }
}

/// <summary>A tile that acts like a link or button: it lights up on hover and calls <paramref name="onPressed"/>.</summary>
public sealed class CarbonClickableTile(Widget child, Action onPressed, bool onLayer = false, float? width = null, float? height = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        return new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: width, height: height, padding: EdgeInsets.All(16),
            color: st.Hover ? (onLayer ? c.LayerHover02 : c.LayerHover01) : onLayer ? c.Layer02 : c.Layer01,
            child: new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                new Expanded(DefaultTextStyle.Merge(ctx, theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary)), child)),
                new Padding(EdgeInsets.Only(left: 16), new Align(Alignment.BottomRight, new Icon(Icons.ChevronRight, 16, c.IconPrimary))),
            ]))), onPressed);
    }
}

/// <summary>A tile that is selected by clicking it, with a check in the corner when it is.</summary>
public sealed class CarbonSelectableTile(Widget child, bool selected, Action<bool> onChanged, bool onLayer = false, float? width = null, float? height = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        return new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: width, height: height,
            decoration: new BoxDecoration(Color: st.Hover ? (onLayer ? c.LayerHover02 : c.LayerHover01) : onLayer ? c.Layer02 : c.Layer01,
                Border: Border.All(selected ? c.IconPrimary : Colors.Transparent, 1)),
            child: new Stack([
                new Padding(EdgeInsets.Only(left: 16, top: 16, bottom: 16, right: 48), DefaultTextStyle.Merge(ctx, theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary)), child)),
                new Positioned(new Icon(selected ? Icons.CheckCircle : Icons.RadioButtonUnchecked, 16, selected ? c.IconPrimary : c.IconDisabled), top: 16, right: 16),
            ], clip: false))), () => onChanged(!selected));
    }
}

/// <summary>A tile with a short part that is always shown and a longer part revealed by the chevron in its corner.</summary>
public sealed class CarbonExpandableTile(Widget above, Widget below, bool initiallyExpanded = false, bool onLayer = false, Key? key = null) : StatefulWidget(key)
{
    internal Widget Above => above;
    internal Widget Below => below;
    internal bool InitiallyExpanded => initiallyExpanded;
    internal bool OnLayer => onLayer;
    public override State CreateState() => new CarbonExpandableTileState();
}

sealed class CarbonExpandableTileState : State<CarbonExpandableTile>
{
    bool _open;
    public override void InitState() => _open = Widget.InitiallyExpanded;

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var style = theme.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary));
        return new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(
            color: st.Hover ? (Widget.OnLayer ? c.LayerHover02 : c.LayerHover01) : Widget.OnLayer ? c.Layer02 : c.Layer01,
            padding: EdgeInsets.All(16), child: new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                new Expanded(DefaultTextStyle.Merge(ctx, style, new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16,
                    children: [Widget.Above, .. _open ? [Widget.Below] : Array.Empty<Widget>()]))),
                new Padding(EdgeInsets.Only(left: 16), new Align(Alignment.BottomRight, new Icon(_open ? Icons.KeyboardArrowDown : Icons.ChevronRight, 16, c.IconPrimary))),
            ]))), () => SetState(() => _open = !_open));
    }
}

/// <summary>
/// A Carbon structured list: a heading row and rows of cells separated by thin rules. Give <paramref name="onSelected"/> to turn it
/// into a single-choice list with a radio button on each row.
/// </summary>
public sealed class CarbonStructuredList(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<Widget>> rows, bool condensed = false,
    int? selectedIndex = null, Action<int>? onSelected = null, Key? key = null) : StatelessWidget(key)
{
    /// <summary>Rows of plain text cells.</summary>
    public static CarbonStructuredList OfText(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, bool condensed = false,
        int? selectedIndex = null, Action<int>? onSelected = null) =>
        new(headers, rows.Select(r => (IReadOnlyList<Widget>)r.Select(t => (Widget)new Text(t)).ToList()).ToList(), condensed, selectedIndex, onSelected);

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool pick = onSelected is not null;
        float pad = condensed ? 8 : 16;

        Widget Row_(IReadOnlyList<Widget> cells, bool head, SKColor back, Widget? lead) => new Row(crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            ..pick ? [new SizedBox(width: 48, child: lead ?? new SizedBox())] : Array.Empty<Widget>(),
            ..cells.Select(cell => (Widget)new Expanded(new Padding(EdgeInsets.Only(left: 16, right: 16, top: pad, bottom: pad),
                DefaultTextStyle.Merge(context, (head ? theme.Type.HeadingCompact01 : theme.Type.Body01).Merge(new TextStyle(Color: c.TextPrimary)), cell)))),
        ]);

        var lines = new List<Widget>
        {
            new Container(color: Colors.Transparent, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            [
                Row_(headers.Select(h => (Widget)new Text(h)).ToList(), true, Colors.Transparent, null),
                new Container(height: 1, color: c.BorderStrong01),
            ])),
        };
        for (int n = 0; n < rows.Count; n++)
        {
            int i = n;
            bool chosen = selectedIndex == i;
            Widget content = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            [
                Row_(rows[i], false, Colors.Transparent, pick ? new Padding(EdgeInsets.Only(left: 16, top: pad + 1), new CarbonRadioButton(chosen, () => onSelected!(i))) : null),
                new Container(height: 1, color: c.BorderSubtle01),
            ]);
            lines.Add(pick ? new Interactive((ctx, st) => new Container(color: st.Hover ? c.LayerHover01 : Colors.Transparent, child: content),
                () => onSelected!(i), focusable: false) : content);
        }
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: lines);
    }
}

public enum CarbonCodeType { Inline, Single, Multi }

/// <summary>
/// A Carbon code snippet. <see cref="CarbonCodeType.Inline"/> sits in a line of text, <see cref="CarbonCodeType.Single"/> is one scrolling line, and
/// <see cref="CarbonCodeType.Multi"/> shows lines with a Show more button once there are more than <paramref name="collapsedLines"/>. All of them copy to the clipboard.
/// </summary>
public sealed class CarbonCodeSnippet(string code, CarbonCodeType type = CarbonCodeType.Single, int collapsedLines = 15, Key? key = null) : StatefulWidget(key)
{
    internal string Code => code;
    internal CarbonCodeType Type => type;
    internal int CollapsedLines => collapsedLines;
    public override State CreateState() => new CarbonCodeSnippetState();
}

sealed class CarbonCodeSnippetState : State<CarbonCodeSnippet>
{
    bool _copied, _expanded;
    int _copyToken;

    void Copy()
    {
        WidgetsBinding.Instance.SetClipboard(Widget.Code);
        int token = ++_copyToken;
        SetState(() => _copied = true);
        WidgetsBinding.Instance.ScheduleTimer(TimeSpan.FromMilliseconds(1500), () => { if (Mounted && token == _copyToken) SetState(() => _copied = false); });
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var style = theme.Type.Code01.Merge(new TextStyle(Color: c.TextPrimary));

        Widget CopyButton() => new Interactive((ctx, st) => new Container(width: 32, height: 32, color: st.Hover ? c.LayerHover01 : Colors.Transparent,
            child: CarbonFocus.Around(st.FocusVisible, theme, new Center(new Icon(_copied ? Icons.Check : Icons.ContentCopy, 16, c.IconPrimary)))), Copy);

        switch (Widget.Type)
        {
            case CarbonCodeType.Inline:
                return new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(
                    padding: EdgeInsets.Symmetric(horizontal: 4), decoration: new BoxDecoration(Color: st.Hover ? c.LayerHover01 : c.Layer01, BorderRadius: BorderRadius.Circular(4)),
                    child: new Text(Widget.Code, style: style, softWrap: false))), Copy, cursor: MouseCursor.Click);

            case CarbonCodeType.Single:
                return new Container(height: 40, color: c.Layer01, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    new Expanded(new SingleChildScrollView(new Padding(EdgeInsets.Symmetric(horizontal: 16), new Align(Alignment.CenterLeft, new Text(Widget.Code, style: style, softWrap: false))), Axis.Horizontal)),
                    new Padding(EdgeInsets.Only(right: 4), CopyButton()),
                ]));

            default:
                var lines = Widget.Code.Split('\n');
                bool long_ = lines.Length > Widget.CollapsedLines;
                var shown = long_ && !_expanded ? string.Join('\n', lines.Take(Widget.CollapsedLines)) : Widget.Code;
                return new Container(color: c.Layer01, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Stack([
                        new SingleChildScrollView(new Padding(EdgeInsets.Only(left: 16, top: 16, right: 56, bottom: 16), new Text(shown, style: style, softWrap: false)), Axis.Horizontal),
                        new Positioned(CopyButton(), top: 8, right: 8),
                    ], clip: false),
                    ..long_ ? [new Padding(EdgeInsets.Only(left: 8, bottom: 8), new Align(Alignment.CenterLeft, new CarbonButton(new Text(_expanded ? "Show less" : "Show more"),
                        () => SetState(() => _expanded = !_expanded), CarbonButtonKind.Ghost, CarbonButtonSize.Small, _expanded ? Icons.KeyboardArrowDown : Icons.ChevronRight)))] : Array.Empty<Widget>(),
                ]));
        }
    }
}

public sealed record CarbonTab(string Label, Widget Content, bool Disabled = false);

/// <summary>Carbon tabs: a row of labels over the content of the selected one. <paramref name="contained"/> uses the filled style on a layer instead of an underline.</summary>
public sealed class CarbonTabs(IReadOnlyList<CarbonTab> tabs, int selectedIndex, Action<int> onChanged, bool contained = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        float height = contained ? 48 : 40;
        var labels = tabs.Select((t, n) =>
        {
            int i = n;
            bool on = i == selectedIndex;
            return (Widget)new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(height: height, padding: EdgeInsets.Symmetric(horizontal: 16),
                constraints: new BoxConstraints(contained ? 160 : 0, float.PositiveInfinity, 0, float.PositiveInfinity),
                decoration: new BoxDecoration(
                    Color: contained ? (on ? c.Background : st.Hover && !t.Disabled ? c.LayerHover01 : c.Layer01) : st.Hover && !t.Disabled && !on ? c.BackgroundHover : Colors.Transparent,
                    Border: Border.Only(bottom: new BorderSide(on && !contained ? c.BorderInverse : Colors.Transparent, 2), top: new BorderSide(on && contained ? c.Interactive : Colors.Transparent, 2))),
                child: new Center(new Text(t.Label, softWrap: false, maxLines: 1, style: theme.Type.BodyCompact01.Merge(new TextStyle(
                    Color: t.Disabled ? c.TextDisabled : on ? c.TextPrimary : c.TextSecondary, FontWeight: on && !contained ? FontWeight.W600 : FontWeight.W400)), textAlign: TextAlign.Center), 1))),
                t.Disabled ? null : () => onChanged(i), focusable: !t.Disabled);
        }).ToList();

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
        [
            new Container(decoration: new BoxDecoration(Border: contained ? default : Border.Only(bottom: new BorderSide(c.BorderSubtle01, 1))),
                child: new SingleChildScrollView(new Row(mainAxisSize: MainAxisSize.Min, spacing: contained ? 1 : 0, children: labels), Axis.Horizontal)),
            tabs[Math.Clamp(selectedIndex, 0, tabs.Count - 1)].Content,
        ]);
    }
}

public sealed record CarbonListItem(string Label, Action? OnPressed = null, IconData? Icon = null);

/// <summary>A Carbon contained list: a titled box of rows, optionally with an action in the header.</summary>
public sealed class CarbonContainedList(string label, IReadOnlyList<CarbonListItem> items, Widget? action = null, bool onLayer = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var back = onLayer ? c.Layer02 : c.Layer01;
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
        [
            new Container(height: 48, color: back, padding: EdgeInsets.Symmetric(horizontal: 16), child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Expanded(new Text(label, style: theme.Type.HeadingCompact01.Merge(new TextStyle(Color: c.TextPrimary)))),
                ..action is null ? Array.Empty<Widget>() : [action],
            ])),
            ..items.Select(item => (Widget)new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            [
                new Container(height: 1, color: c.BorderSubtle01),
                new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(height: 40, color: st.Hover && item.OnPressed is not null ? (onLayer ? c.LayerHover02 : c.LayerHover01) : back,
                    padding: EdgeInsets.Symmetric(horizontal: 16), child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                    [
                        ..item.Icon is null ? Array.Empty<Widget>() : [new Icon(item.Icon, 16, c.IconPrimary)],
                        new Expanded(new Text(item.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary)))),
                    ]))), item.OnPressed, cursor: item.OnPressed is null ? MouseCursor.Default : MouseCursor.Click, focusable: item.OnPressed is not null),
            ])),
        ]);
    }
}

public sealed record CarbonTreeNode(string Id, string Label, IReadOnlyList<CarbonTreeNode>? Children = null, IconData? Icon = null, bool Disabled = false);

/// <summary>A Carbon tree view: nested nodes that expand and collapse, with one selected node. The chevron toggles; clicking the row selects (and toggles a parent).</summary>
public sealed class CarbonTreeView(IReadOnlyList<CarbonTreeNode> nodes, string? selectedId = null, Action<CarbonTreeNode>? onSelected = null,
    IReadOnlyCollection<string>? initiallyExpanded = null, bool compact = false, Key? key = null) : StatefulWidget(key)
{
    internal IReadOnlyList<CarbonTreeNode> Nodes => nodes;
    internal string? SelectedId => selectedId;
    internal Action<CarbonTreeNode>? OnSelected => onSelected;
    internal IReadOnlyCollection<string>? InitiallyExpanded => initiallyExpanded;
    internal bool Compact => compact;
    public override State CreateState() => new CarbonTreeViewState();
}

sealed class CarbonTreeViewState : State<CarbonTreeView>
{
    readonly HashSet<string> _open = new();

    public override void InitState()
    {
        foreach (var id in Widget.InitiallyExpanded ?? []) _open.Add(id);
    }

    void Toggle(string id) => SetState(() => { if (!_open.Remove(id)) _open.Add(id); });

    void Emit(CarbonTreeNode node, List<Widget> into, int depth, CarbonThemeData theme)
    {
        var c = theme.Colors;
        bool parent = node.Children is { Count: > 0 };
        bool open = parent && _open.Contains(node.Id);
        bool selected = Widget.SelectedId == node.Id;
        float h = Widget.Compact ? 24 : 32;
        into.Add(new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(height: h,
            decoration: new BoxDecoration(Color: selected ? c.LayerSelected01 : st.Hover && !node.Disabled ? c.LayerHover01 : Colors.Transparent,
                Border: Border.Only(left: new BorderSide(selected ? c.BorderInteractive : Colors.Transparent, 3))),
            padding: EdgeInsets.Only(left: 13 + depth * 16),
            child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 4, children:
            [
                new SizedBox(width: 16, child: parent ? new GestureDetector(onTap: node.Disabled ? null : () => Toggle(node.Id), behavior: HitTestBehavior.Opaque,
                    child: new Icon(open ? Icons.KeyboardArrowDown : Icons.ChevronRight, 16, node.Disabled ? c.IconDisabled : c.IconSecondary)) : null),
                ..node.Icon is null ? Array.Empty<Widget>() : [new Icon(node.Icon, 16, node.Disabled ? c.IconDisabled : c.IconSecondary)],
                new Expanded(new Text(node.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                    style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: node.Disabled ? c.TextDisabled : selected ? c.TextPrimary : c.TextSecondary)))),
            ]))), node.Disabled ? null : () => { if (parent) Toggle(node.Id); Widget.OnSelected?.Invoke(node); }, focusable: !node.Disabled));
        if (open) foreach (var child in node.Children!) Emit(child, into, depth + 1, theme);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var rows = new List<Widget>();
        foreach (var n in Widget.Nodes) Emit(n, rows, 0, theme);
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: rows);
    }
}

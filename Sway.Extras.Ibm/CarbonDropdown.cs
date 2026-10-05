using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

public sealed record CarbonDropdownItem<T>(T Value, string Label, bool Enabled = true);

/// <summary>
/// A Carbon dropdown: a filled field that opens a menu of options directly beneath it. Arrow keys, Enter and Escape work
/// while it is open. A null <c>onChanged</c> disables it.
/// </summary>
public sealed class CarbonDropdown<T>(IReadOnlyList<CarbonDropdownItem<T>> items, T? value = default, Action<T>? onChanged = null,
    string? label = null, string? placeholder = null, CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false,
    float menuMaxHeight = 240, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<CarbonDropdownItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T>? OnChanged => onChanged;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    internal float MenuMaxHeight => menuMaxHeight;
    public override State CreateState() => new CarbonDropdownState<T>();
}

sealed class CarbonDropdownState<T> : State<CarbonDropdown<T>> where T : notnull
{
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "CarbonDropdown" };

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
        if (refocus && Mounted) _node.RequestFocus();
    }

    void Open()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box) return;
        var overlay = Overlay.MaybeOf(Context);
        if (overlay is null) return;

        var origin = box.LocalToGlobal(Offset.Zero);
        var size = box.Size;
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        float item = (float)Widget.Size;
        float menuHeight = Math.Min(Widget.MenuMaxHeight, Widget.Items.Count * item);
        // Carbon opens downward and only flips when there is no room beneath.
        bool above = origin.Dy + size.Height + menuHeight > window.Height && origin.Dy - menuHeight > 0;
        float top = above ? origin.Dy - menuHeight : origin.Dy + size.Height;
        var owner = Context;

        _entry = new OverlayEntry(ctx => CarbonTheme.Wrap(owner, new Stack([
            Positioned.Fill(new GestureDetector(onTap: () => Close(), behavior: HitTestBehavior.Opaque)),
            new Positioned(new CarbonDropdownMenu<T>(Widget.Items, Widget.Value, v => { Close(); Widget.OnChanged?.Invoke(v); }, () => Close(), menuHeight, item),
                left: origin.Dx, top: top, width: size.Width),
        ], fit: StackFit.Expand, clip: false)));
        overlay.Insert(_entry);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var selected = Widget.Items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        bool enabled = Widget.OnChanged is not null;
        bool open = _entry is not null;
        float height = (float)Widget.Size;

        Widget field = new Interactive((ctx, st) =>
        {
            Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Expanded(new Padding(EdgeInsets.Only(left: 16), new Text(selected?.Label ?? Widget.Placeholder ?? CarbonLocalizations.Of(context).Select, softWrap: false,
                    overflow: TextOverflow.Ellipsis, maxLines: 1,
                    style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: !enabled ? c.TextDisabled : selected is null ? c.TextPlaceholder : c.TextPrimary))))),
                new SizedBox(width: height, height: height, child: new Center(new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 16, enabled ? c.IconPrimary : c.IconDisabled))),
            ]);
            return CarbonField.Frame(theme, height, open || st.FocusVisible, st.Hover && enabled, false, enabled, Widget.OnLayer ? c.Field02 : null, content);
        }, enabled ? () => { if (_entry is null) Open(); else Close(); } : null, focusNode: _node);

        if (Widget.Label is null) return field;
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled))),
            field,
        ]);
    }
}

sealed class CarbonDropdownMenu<T>(IReadOnlyList<CarbonDropdownItem<T>> items, T? value, Action<T> onSelect, Action onClose, float height, float itemHeight)
    : StatefulWidget where T : notnull
{
    internal IReadOnlyList<CarbonDropdownItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float Height => height;
    internal float ItemHeight => itemHeight;
    public override State CreateState() => new CarbonDropdownMenuState<T>();
}

sealed class CarbonDropdownMenuState<T> : State<CarbonDropdownMenu<T>> where T : notnull
{
    int _highlight = -1;
    readonly ScrollController _scroll = new();

    public override void InitState()
    {
        _highlight = Widget.Items.ToList().FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value));
        if (_highlight < 0) _highlight = Next(-1, 1);
    }

    int Next(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < Widget.Items.Count; i += step)
            if (Widget.Items[i].Enabled) return i;
        return from;
    }

    void Highlight(int index)
    {
        if (index < 0) return;
        SetState(() => _highlight = index);
        float ih = Widget.ItemHeight, top = index * ih, pos = _scroll.Offset;
        if (top < pos) _scroll.JumpTo(top);
        else if (top + ih > pos + Widget.Height) _scroll.JumpTo(top + ih - Widget.Height);
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return true;
        switch (e.Key)
        {
            case "ArrowDown": Highlight(Next(_highlight, 1)); break;
            case "ArrowUp": Highlight(Next(_highlight, -1)); break;
            case "Home": Highlight(Next(-1, 1)); break;
            case "End": Highlight(Next(Widget.Items.Count, -1)); break;
            case "Enter" or " ":
                if (_highlight >= 0) Widget.OnSelect(Widget.Items[_highlight].Value);
                break;
            case "Escape" or "Tab": Widget.OnClose(); break;
        }
        return true; // the menu is modal: swallow keys
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            var item = Widget.Items[i];
            bool selected = EqualityComparer<T>.Default.Equals(item.Value, Widget.Value);
            rows.Add(new SizedBox(height: Widget.ItemHeight, child: new Interactive((ctx, st) => new Container(
                    padding: EdgeInsets.Symmetric(horizontal: 16),
                    color: _highlight == index || st.Hover ? c.LayerHover01 : Colors.Transparent,
                    child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                    [
                        new Expanded(new Text(item.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                            style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: item.Enabled ? c.TextPrimary : c.TextDisabled)))),
                        ..selected ? [new Icon(Icons.Check, 16, c.IconPrimary)] : Array.Empty<Widget>(),
                    ])),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false)));
        }

        return new Focus(autofocus: true, onKey: OnKey, child: new Container(
            height: Widget.Height,
            decoration: new BoxDecoration(Color: c.Layer01, Border: Border.All(c.BorderSubtle01),
                BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.3f), new Offset(0, 2), 6)]),
            child: new SingleChildScrollView(new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch), controller: _scroll)));
    }
}

/// <summary>Carbon's content switcher: a row of equal-width segments where exactly one is selected.</summary>
public sealed class ContentSwitcher(IReadOnlyList<string> labels, int selectedIndex, Action<int> onChanged,
    CarbonButtonSize size = CarbonButtonSize.Medium, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var selectedStyle = theme.Type.BodyCompact01.Merge(new TextStyle(FontWeight: FontWeight.W600));
        // Every segment is as wide as the widest label (in its bold form) plus padding.
        float width = labels.Max(l => RenderParagraph.Measure(l, selectedStyle, selectedStyle.ToFont())) + 32;

        return new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children: labels.Select((label, i) =>
        {
            bool on = i == selectedIndex;
            return (Widget)new SizedBox(width: width, child: new Interactive((ctx, st) =>
            {
                var bg = on ? c.BackgroundInverse : st.Hover ? c.LayerHover01 : c.Layer01;
                var fg = on ? c.TextInverse : c.TextSecondary;
                Widget body = new Container(height: (float)size, color: bg,
                    child: new Center(new Text(label, softWrap: false, maxLines: 1,
                        style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: fg, FontWeight: on ? FontWeight.W600 : FontWeight.W400)))));
                return CarbonFocus.Around(st.FocusVisible, theme, body);
            }, () => onChanged(i)));
        }).ToList());
    }
}

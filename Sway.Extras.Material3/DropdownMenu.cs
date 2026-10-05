using Sway.Widgets;

namespace Sway.Extras.Material3;

sealed class DropdownMenu<T>(IReadOnlyList<DropdownMenuItem<T>> items, T? value, Action<T> onSelect, Action onClose, float height, float itemHeight) : StatefulWidget
    where T : notnull
{
    internal IReadOnlyList<DropdownMenuItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float Height => height;
    internal float ItemHeight => itemHeight;
    public override State CreateState() => new DropdownMenuState<T>();
}

sealed class DropdownMenuState<T> : State<DropdownMenu<T>> where T : notnull
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
        float ih = Widget.ItemHeight, top = 8 + index * ih, pos = _scroll.Offset;
        if (top < pos) _scroll.JumpTo(top - 8);
        else if (top + ih > pos + Widget.Height - 8) _scroll.JumpTo(top + ih - Widget.Height + 8);
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
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            var item = Widget.Items[i];
            bool selected = EqualityComparer<T>.Default.Equals(item.Value, Widget.Value);
            rows.Add(new SizedBox(height: Widget.ItemHeight, child: new Interactive((ctx, st) => new Container(
                    padding: EdgeInsets.Symmetric(12, 0), alignment: Alignment.CenterLeft,
                    color: StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, s.OnSurface, _highlight == index ? 0.10f : StateLayer.Opacity(st)),
                    child: DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: item.Enabled ? s.OnSurface : s.OnSurface.WithOpacity(0.38f))), item.Child)),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false)));
        }

        return new Focus(autofocus: true, onKey: OnKey, child: new Container(
            height: Widget.Height,
            padding: EdgeInsets.Symmetric(vertical: 8),
            decoration: new BoxDecoration(Color: s.SurfaceContainer, BorderRadius: BorderRadius.Circular(Shapes.ExtraSmall),
                BoxShadow: Elevation.Shadows(2, s.Shadow)),
            child: new ClipRRect(BorderRadius.Circular(Shapes.ExtraSmall), new SingleChildScrollView(
                new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch), controller: _scroll))));
    }
}

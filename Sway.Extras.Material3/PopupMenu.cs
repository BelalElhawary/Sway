using Sway.Widgets;

namespace Sway.Extras.Material3;

sealed class PopupMenu<T>(IReadOnlyList<MenuEntry<T>> items, Action<T> onSelect, Action onClose, float minWidth) : StatefulWidget
{
    internal IReadOnlyList<MenuEntry<T>> Items => items;
    internal Action<T> OnSelect => onSelect;
    internal Action OnClose => onClose;
    internal float MinWidth => minWidth;
    public override State CreateState() => new PopupMenuState<T>();
}

sealed class PopupMenuState<T> : State<PopupMenu<T>>
{
    int _highlight = -1;

    bool IsEnabledItem(int i) => Widget.Items[i] is MenuItem<T> { Enabled: true };

    int Next(int from, int step)
    {
        for (int i = from + step; i >= 0 && i < Widget.Items.Count; i += step)
            if (IsEnabledItem(i)) return i;
        return from;
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return true;
        switch (e.Key)
        {
            case "ArrowDown": SetState(() => _highlight = Next(_highlight < 0 ? -1 : _highlight, 1)); break;
            case "ArrowUp": SetState(() => _highlight = Next(_highlight < 0 ? Widget.Items.Count : _highlight, -1)); break;
            case "Home": SetState(() => _highlight = Next(-1, 1)); break;
            case "End": SetState(() => _highlight = Next(Widget.Items.Count, -1)); break;
            case "Enter" or " ":
                if (_highlight >= 0 && Widget.Items[_highlight] is MenuItem<T> item) Widget.OnSelect(item.Value);
                break;
            case "Escape" or "Tab": Widget.OnClose(); break;
        }
        return true; // modal: swallow keys
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var rows = new List<Widget>();
        for (int i = 0; i < Widget.Items.Count; i++)
        {
            int index = i;
            if (Widget.Items[i] is MenuDivider<T>)
            {
                rows.Add(new SizedBox(height: Menus.DividerHeight, child: new Center(new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)))));
                continue;
            }
            var item = (MenuItem<T>)Widget.Items[i];
            var fg = item.Enabled ? s.OnSurface : s.OnSurface.WithOpacity(0.38f);
            rows.Add(new SizedBox(height: Menus.ItemHeight, child: new Interactive((ctx, st) => new Container(
                    padding: EdgeInsets.Symmetric(horizontal: 12), alignment: Alignment.CenterLeft,
                    color: StateLayer.Blend(Colors.Transparent, s.OnSurface, _highlight == index ? 0.10f : StateLayer.Opacity(st)),
                    child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 12, children:
                    [
                        ..item.Icon is null ? Array.Empty<Widget>() : [new Icon(item.Icon, 24, item.Enabled ? s.OnSurfaceVariant : fg)],
                        DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), item.Child),
                    ])),
                item.Enabled ? () => Widget.OnSelect(item.Value) : null, focusable: false)));
        }

        return new Focus(autofocus: true, trapFocus: true, skipTraversal: true, onKey: OnKey, child: new ConstrainedBox(
            new BoxConstraints(Widget.MinWidth, Menus.MaxWidth, 0, float.PositiveInfinity),
            new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainer, BorderRadius: BorderRadius.Circular(Shapes.ExtraSmall),
                BoxShadow: Elevation.Shadows(2, s.Shadow)),
                new ClipRRect(BorderRadius.Circular(Shapes.ExtraSmall), new IntrinsicWidth(new Padding(EdgeInsets.Symmetric(vertical: Menus.VerticalPadding),
                    new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch)))))));
    }
}

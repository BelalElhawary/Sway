using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>An icon button in a <see cref="CarbonHeader"/>, such as search, notifications or the account switcher.</summary>
public sealed record CarbonHeaderAction(IconData Icon, Action OnPressed, string? Label = null);

/// <summary>
/// The Carbon UI shell header: a 48px dark bar with an optional menu button, the product name and a row of icon actions. It is dark in every theme,
/// as Carbon's is. <paramref name="onMenu"/> adds the hamburger that opens the side navigation.
/// </summary>
public sealed class CarbonHeader(string name, string? prefix = null, Action? onMenu = null, IReadOnlyList<CarbonHeaderAction>? actions = null,
    Key? key = null) : StatelessWidget(key)
{
    static readonly SKColor Back = Colors.FromRgb(0x161616), Line = Colors.FromRgb(0x393939), Hover = Colors.FromRgb(0x2C2C2C), Text_ = Colors.FromRgb(0xF4F4F4);

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);

        Widget Square(IconData icon, Action onPressed) => new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: 48, height: 48,
            color: st.Pressed ? Colors.FromRgb(0x393939) : st.Hover ? Hover : Colors.Transparent, child: new Center(new Icon(icon, 20, Text_))), Colors.White), onPressed);

        return new Container(height: 48, decoration: new BoxDecoration(Color: Back, Border: Border.Only(bottom: new BorderSide(Line, 1))),
            child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                ..onMenu is null ? Array.Empty<Widget>() : [Square(Icons.Menu, onMenu)],
                new Padding(EdgeInsets.Symmetric(horizontal: 16), new Row(mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    ..prefix is null ? Array.Empty<Widget>() : [new Text(prefix, style: theme.Type.Heading01.Merge(new TextStyle(Color: Text_, FontWeight: FontWeight.W600)))],
                    new Text(name, style: theme.Type.Body01.Merge(new TextStyle(Color: Text_))),
                ])),
                new Expanded(new SizedBox()),
                ..(actions ?? []).Select(a => Square(a.Icon, a.OnPressed)),
            ]));
    }
}

public sealed record CarbonNavItem(string Id, string Label, IconData? Icon = null, IReadOnlyList<CarbonNavItem>? Children = null);

/// <summary>
/// The Carbon side navigation: a 256px column of links. An item with children is a category that expands; the selected link gets a bar on its left edge.
/// </summary>
public sealed class CarbonSideNav(IReadOnlyList<CarbonNavItem> items, string? selectedId, Action<CarbonNavItem> onSelected,
    IReadOnlyCollection<string>? initiallyExpanded = null, float width = 256, Key? key = null) : StatefulWidget(key)
{
    internal IReadOnlyList<CarbonNavItem> Items => items;
    internal string? SelectedId => selectedId;
    internal Action<CarbonNavItem> OnSelected => onSelected;
    internal IReadOnlyCollection<string>? InitiallyExpanded => initiallyExpanded;
    internal float PanelWidth => width;
    public override State CreateState() => new CarbonSideNavState();
}

sealed class CarbonSideNavState : State<CarbonSideNav>
{
    readonly HashSet<string> _open = new();

    public override void InitState()
    {
        foreach (var id in Widget.InitiallyExpanded ?? []) _open.Add(id);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var rows = new List<Widget>();

        void Add(CarbonNavItem item, int depth)
        {
            bool category = item.Children is { Count: > 0 };
            bool open = category && _open.Contains(item.Id);
            bool selected = Widget.SelectedId == item.Id;
            rows.Add(new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(height: 32,
                decoration: new BoxDecoration(Color: selected ? c.BackgroundSelected : st.Hover ? c.BackgroundHover : Colors.Transparent,
                    Border: Border.Only(left: new BorderSide(selected ? c.BorderInteractive : Colors.Transparent, 4))),
                padding: EdgeInsets.Only(left: 12 + depth * 16, right: 16), child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 12, children:
                [
                    ..item.Icon is null ? Array.Empty<Widget>() : [new Icon(item.Icon, 16, c.IconSecondary)],
                    new Expanded(new Text(item.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                        style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: selected ? c.TextPrimary : c.TextSecondary, FontWeight: selected ? FontWeight.W600 : FontWeight.W400)))),
                    ..category ? [new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 16, c.IconSecondary)] : Array.Empty<Widget>(),
                ]))), () =>
            {
                if (category) SetState(() => { if (!_open.Remove(item.Id)) _open.Add(item.Id); });
                else Widget.OnSelected(item);
            }));
            if (open) foreach (var child in item.Children!) Add(child, depth + 1);
        }

        foreach (var item in Widget.Items) Add(item, 0);
        return new Container(width: Widget.PanelWidth, decoration: new BoxDecoration(Color: c.Layer01, Border: Border.Only(right: new BorderSide(c.BorderSubtle01, 1))),
            padding: EdgeInsets.Symmetric(vertical: 16), child: new SingleChildScrollView(new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: rows)));
    }
}

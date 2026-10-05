using System.Reflection;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>Every bundled Material icon in a lazily built grid, with a search box and a style picker.</summary>
class IconsPage : StatefulWidget
{
    public override State CreateState() => new IconsPageState();
}

class IconsPageState : State<IconsPage>
{
    static readonly (string id, string label, Type type)[] Styles =
    [
        ("filled", "Filled", typeof(Icons)), ("outlined", "Outlined", typeof(Icons.Outlined)), ("round", "Round", typeof(Icons.Round)),
        ("sharp", "Sharp", typeof(Icons.Sharp)), ("twotone", "Two-tone", typeof(Icons.TwoTone)),
    ];

    static readonly Dictionary<string, List<(string name, IconData icon)>> Loaded = new();

    static List<(string name, IconData icon)> IconsOf(string style)
    {
        if (!Loaded.TryGetValue(style, out var list))
        {
            var type = Styles.First(s => s.id == style).type;
            Loaded[style] = list = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(IconData))
                .Select(p => (p.Name.TrimStart('_'), (IconData)p.GetValue(null)!))
                .OrderBy(i => i.Item1, StringComparer.Ordinal)
                .ToList();
        }
        return list;
    }

    readonly TextEditingController _search = new();
    string _style = "filled";

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        string query = _search.Text.Trim().Replace(" ", "").Replace("_", "");
        var all = IconsOf(_style);
        var shown = query.Length == 0 ? all : all.Where(i => i.name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return new LayoutBuilder((ctx, box) =>
        {
            float pad = Ui.IsCompact(box) ? 12 : 24;
            return new Padding(EdgeInsets.All(pad), new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, spacing: 12, children:
            [
                new Text("Icons", style: theme.TextTheme.HeadlineMedium),
                new Wrap(spacing: 16, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new SizedBox(width: 320, child: new SearchBar(_search, "Search icons", _ => SetState(() => { }))),
                    new SizedBox(width: 180, child: new DropdownButton<string>(
                        Styles.Select(st => new DropdownMenuItem<string>(st.id, new Text(st.label))).ToList(),
                        _style, v => SetState(() => _style = v), label: "Style")),
                    new Text($"{shown.Count} of {all.Count}", style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                ]),
                new Expanded(shown.Count == 0
                    ? new Center(new Text("No icons match", style: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))))
                    : GridView.Builder(shown.Count, (c, i) => Cell(c, shown[i].name, shown[i].icon),
                        minColumnWidth: 112, mainAxisExtent: 96, crossAxisSpacing: 8, mainAxisSpacing: 8, padding: EdgeInsets.Only(bottom: 8))),
            ]));
        });
    }

    static Widget Cell(BuildContext context, string name, IconData icon)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new Container(padding: EdgeInsets.Symmetric(6, 8),
            decoration: new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: BorderRadius.Circular(12)),
            child: new Column(mainAxisAlignment: MainAxisAlignment.Center, spacing: 8, children:
            [
                new Icon(icon, 32, s.OnSurface),
                new Text(name, style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnSurfaceVariant, FontSize: 11)),
                    textAlign: TextAlign.Center, overflow: TextOverflow.Ellipsis, maxLines: 2),
            ]));
    }
}

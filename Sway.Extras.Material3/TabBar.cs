using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Primary tabs: equal-width tabs, or scrollable ones sized to their content, with an underline for the selected tab.</summary>
public sealed class TabBar(IReadOnlyList<Tab> tabs, int selectedIndex, Action<int>? onTap = null, bool isScrollable = false, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool both = tabs.Any(t => t.Icon is not null && t.Text is not null);
        float height = both ? 64 : 48;

        Widget TabCell(int index)
        {
            var tab = tabs[index];
            bool selected = index == selectedIndex;
            return new Interactive((ctx, st) =>
            {
                var fg = selected ? s.Primary : s.OnSurfaceVariant;
                var bg = StateLayer.Blend(Colors.Transparent, selected ? s.Primary : s.OnSurface, StateLayer.Opacity(st));
                return new Container(height: height, color: bg, padding: EdgeInsets.Symmetric(horizontal: isScrollable ? 16 : 8),
                    child: new Column(mainAxisAlignment: MainAxisAlignment.End, children:
                    [
                        new Expanded(new Center(new Column(mainAxisSize: MainAxisSize.Min, spacing: 2, children:
                        [
                            ..tab.Icon is null ? Array.Empty<Widget>() : [new Icon(tab.Icon, 24, fg)],
                            ..tab.Text is null ? Array.Empty<Widget>()
                                : [new Text(tab.Text, style: theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: fg)))],
                        ]))),
                        new AnimatedContainer(TimeSpan.FromMilliseconds(200), height: selected ? 3 : 0, margin: EdgeInsets.Symmetric(horizontal: 4),
                            decoration: new BoxDecoration(Color: s.Primary, BorderRadius: new BorderRadius(new Radius(3, 3), new Radius(3, 3), new Radius(0, 0), new Radius(0, 0)))),
                    ]));
            }, onTap is null ? null : () => onTap(index));
        }

        var cells = Enumerable.Range(0, tabs.Count).Select(TabCell).ToList();
        Widget row = isScrollable
            ? new SingleChildScrollView(new Row(cells, mainAxisSize: MainAxisSize.Min), Axis.Horizontal)
            : new Row(cells.Select(c => (Widget)new Expanded(c)).ToList(), crossAxisAlignment: CrossAxisAlignment.Stretch);
        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            row,
            new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)),
        ]);
    }
}

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The content of a Material 3 navigation drawer: an optional header and a list of destinations.</summary>
public sealed class NavigationDrawer(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null,
    Widget? header = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = new BorderRadius(new Radius(0, 0), new Radius(16, 16), new Radius(16, 16), new Radius(0, 0));
        var rows = new List<Widget>();
        if (header is not null)
            rows.Add(new Padding(EdgeInsets.Symmetric(28, 16), DefaultTextStyle.Merge(context, theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), header)));
        for (int i = 0; i < destinations.Count; i++)
        {
            int index = i;
            var d = destinations[i];
            bool selected = i == selectedIndex;
            rows.Add(new Padding(EdgeInsets.Symmetric(horizontal: 12), new Interactive((ctx, st) =>
            {
                var fg = selected ? s.OnSecondaryContainer : s.OnSurfaceVariant;
                return new AnimatedContainer(TimeSpan.FromMilliseconds(150), height: 56, padding: EdgeInsets.Symmetric(horizontal: 16),
                    decoration: new BoxDecoration(
                        Color: StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, fg, StateLayer.Opacity(st)),
                        BorderRadius: BorderRadius.Circular(28)),
                    child: new Row(spacing: 12, children:
                    [
                        new Icon(selected ? d.SelectedIcon ?? d.Icon : d.Icon, 24, fg),
                        new Text(d.Label, style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg))),
                    ]));
            }, onDestinationSelected is null ? null : () => onDestinationSelected(index))));
        }
        return new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: radius, BoxShadow: Elevation.Shadows(1, s.Shadow)),
            new ClipRRect(radius, new SingleChildScrollView(
                new Padding(EdgeInsets.Symmetric(vertical: 12), new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch)))));
    }
}

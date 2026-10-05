using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class NavigationRail(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null,
    Widget? leading = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var items = new List<Widget>();
        if (leading is not null) items.Add(new Padding(EdgeInsets.Symmetric(vertical: 8), leading));
        for (int i = 0; i < destinations.Count; i++)
        {
            int index = i;
            var d = destinations[i];
            bool sel = i == selectedIndex;
            items.Add(new Interactive((ctx, st) => new Padding(EdgeInsets.Symmetric(vertical: 6), new Column(mainAxisSize: MainAxisSize.Min, spacing: 4, children:
            [
                new AnimatedContainer(TimeSpan.FromMilliseconds(200), width: 56, height: 32, alignment: Alignment.Center, curve: Curves.EaseOutCubic,
                    decoration: new BoxDecoration(
                        Color: sel ? s.SecondaryContainer.WithOpacity(1) : StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)),
                        BorderRadius: BorderRadius.Circular(16)),
                    child: new Icon(sel ? d.SelectedIcon ?? d.Icon : d.Icon, 24, sel ? s.OnSecondaryContainer : s.OnSurfaceVariant)),
                new Text(d.Label, style: new TextStyle(Color: sel ? s.OnSurface : s.OnSurfaceVariant, FontWeight: sel ? FontWeight.W600 : FontWeight.W500)
                    .Merge(new TextStyle(FontSize: theme.TextTheme.LabelMedium.FontSize, LetterSpacing: theme.TextTheme.LabelMedium.LetterSpacing))),
            ])), () => onDestinationSelected?.Invoke(index)));
        }
        return new Container(width: 80, color: s.Surface, padding: EdgeInsets.Symmetric(vertical: 8),
            child: new Column(mainAxisSize: MainAxisSize.Max, crossAxisAlignment: CrossAxisAlignment.Center, children: items));
    }
}

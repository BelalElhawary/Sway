using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class NavigationBar(int selectedIndex, IReadOnlyList<NavigationDestination> destinations, Action<int>? onDestinationSelected = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new Container(height: 80, color: s.SurfaceContainer, child: new Row(children: destinations.Select((d, i) =>
        {
            bool sel = i == selectedIndex;
            return (Widget)new Expanded(new Interactive((ctx, st) => new Column(mainAxisAlignment: MainAxisAlignment.Center, spacing: 4, children:
            [
                new AnimatedContainer(TimeSpan.FromMilliseconds(200), width: 64, height: 32, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: sel ? s.SecondaryContainer : StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)), BorderRadius: BorderRadius.Circular(16)),
                    child: new Icon(sel ? d.SelectedIcon ?? d.Icon : d.Icon, 24, sel ? s.OnSecondaryContainer : s.OnSurfaceVariant)),
                new Text(d.Label, style: new TextStyle(Color: sel ? s.OnSurface : s.OnSurfaceVariant, FontWeight: sel ? FontWeight.W600 : FontWeight.W500, FontSize: 12)),
            ]), () => onDestinationSelected?.Invoke(i)));
        }).ToList()));
    }
}

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Chip(Widget label, Action? onPressed = null, bool selected = false, IconData? icon = null, Action? onDeleted = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Small);
        return new Interactive((ctx, st) =>
        {
            var fg = selected ? s.OnSecondaryContainer : s.OnSurfaceVariant;
            var bg = StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, selected ? s.OnSecondaryContainer : s.OnSurfaceVariant, StateLayer.Opacity(st));
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 32,
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius, Border: selected ? null : Border.All(s.Outline)),
                // The content swaps instantly; the size eases once, so the chip does not jump wider and then shrink back.
                child: new AnimatedSize(TimeSpan.FromMilliseconds(120), new Padding(EdgeInsets.Symmetric(horizontal: icon is null && !selected ? 16 : 8), new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    ..selected ? [new Icon(Icons.Check, 18, fg)] : icon is not null ? [new Icon(icon, 18, fg)] : Array.Empty<Widget>(),
                    DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)), label),
                    ..onDeleted is null ? Array.Empty<Widget>() : [new GestureDetector(onTap: onDeleted, child: new Icon(Icons.Close, 18, fg))],
                ]))));
            return FocusRing.Around(st.FocusVisible, radius, body, s.Primary);
        }, onPressed);
    }
}

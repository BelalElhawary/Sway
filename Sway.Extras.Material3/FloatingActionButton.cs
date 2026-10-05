using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class FloatingActionButton(Widget? child = null, Action? onPressed = null, Widget? label = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(Shapes.Large);
        bool extended = label is not null;
        return new Interactive((ctx, st) =>
        {
            var bg = StateLayer.Blend(s.PrimaryContainer, s.OnPrimaryContainer, StateLayer.Opacity(st));
            Widget body = new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 56, constraints: new BoxConstraints(56, float.PositiveInfinity, 56, 56),
                padding: EdgeInsets.Symmetric(horizontal: extended ? 16 : 0),
                decoration: new BoxDecoration(Color: bg, BorderRadius: radius, BoxShadow: Elevation.Shadows(st.Hover ? 4 : 3, s.Shadow)),
                child: new IconTheme(s.OnPrimaryContainer, 24, new Row(mainAxisSize: MainAxisSize.Min, mainAxisAlignment: MainAxisAlignment.Center, spacing: 8, children:
                [
                    child ?? new SizedBox(),
                    ..extended ? [DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnPrimaryContainer)), label!)] : Array.Empty<Widget>(),
                ])));
            return FocusRing.Around(st.FocusVisible, radius, body, s.Primary);
        }, onPressed);
    }
}

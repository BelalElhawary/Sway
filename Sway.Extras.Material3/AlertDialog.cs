using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class AlertDialog(Widget? title = null, Widget? content = null, IReadOnlyList<Widget>? actions = null, IconData? icon = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new ConstrainedBox(new BoxConstraints(280, 560, 0, float.PositiveInfinity), new Material(new Padding(EdgeInsets.All(24),
            new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
            [
                ..icon is null ? Array.Empty<Widget>() : [new Align(Alignment.Center, new Padding(EdgeInsets.Only(bottom: 16), new Icon(icon, 24, s.Secondary)))],
                ..title is null ? Array.Empty<Widget>() : [new Padding(EdgeInsets.Only(bottom: 16),
                    DefaultTextStyle.Merge(context, theme.TextTheme.HeadlineSmall.Merge(new TextStyle(Color: s.OnSurface)), title))],
                ..content is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(context, theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)), content)],
                ..actions is null ? Array.Empty<Widget>() : [new Padding(EdgeInsets.Only(top: 24), new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children: actions)))],
            ])), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}

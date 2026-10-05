using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class ListTile(Widget? title = null, Widget? subtitle = null, Widget? leading = null, Widget? trailing = null,
    Action? onTap = null, bool selected = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool two = subtitle is not null;
        return new Interactive((ctx, st) =>
        {
            var bg = StateLayer.Blend(selected ? s.SecondaryContainer : Colors.Transparent, s.OnSurface, onTap is null ? 0 : StateLayer.Opacity(st));
            var content = new Row(spacing: 16, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                ..leading is null ? Array.Empty<Widget>() : [new IconTheme(selected ? s.OnSecondaryContainer : s.OnSurfaceVariant, 24, leading)],
                new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                [
                    DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: selected ? s.OnSecondaryContainer : s.OnSurface)), title ?? new SizedBox()),
                    ..two ? [DefaultTextStyle.Merge(ctx, theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant)), subtitle!)] : Array.Empty<Widget>(),
                ])),
                ..trailing is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(ctx, theme.TextTheme.LabelSmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), trailing)],
            ]);
            return new AnimatedContainer(TimeSpan.FromMilliseconds(100), color: bg, constraints: new BoxConstraints(0, float.PositiveInfinity, two ? 72 : 56, float.PositiveInfinity),
                padding: EdgeInsets.Symmetric(16, 8), child: content);
        }, onTap, cursor: onTap is null ? MouseCursor.Default : MouseCursor.Click, focusable: onTap is not null);
    }
}

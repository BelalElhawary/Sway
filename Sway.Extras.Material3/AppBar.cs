using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class AppBar(Widget? title = null, Widget? leading = null, IReadOnlyList<Widget>? actions = null, SKColor? backgroundColor = null,
    bool centerTitle = false, Key? key = null) : StatelessWidget(key)
{
    public const float Height = 64;

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new Container(height: Height, color: backgroundColor ?? s.Surface, padding: EdgeInsets.Symmetric(horizontal: 4),
            child: new IconTheme(s.OnSurfaceVariant, 24, new Row(children:
            [
                ..(leading is null ? new Widget[] { new SizedBox(width: 12) } : new Widget[] { new SizedBox(48, 48, new Center(leading)), new SizedBox(width: 4) }),
                new Expanded(new Align(centerTitle ? Alignment.Center : AlignmentDirectional.CenterStart,
                    DefaultTextStyle.Merge(context, theme.TextTheme.TitleLarge.Merge(new TextStyle(Color: s.OnSurface)), title ?? new SizedBox()))),
                ..actions ?? Array.Empty<Widget>(),
                new SizedBox(width: 8),
            ])));
    }
}

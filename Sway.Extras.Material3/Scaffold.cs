using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Scaffold(Widget body, AppBar? appBar = null, Widget? floatingActionButton = null, Widget? bottomNavigationBar = null,
    Widget? navigationRail = null, SKColor? backgroundColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        Widget content = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            ..appBar is null ? Array.Empty<Widget>() : [appBar],
            new Expanded(new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                ..navigationRail is null ? Array.Empty<Widget>() : [navigationRail],
                new Expanded(body),
            ])),
            ..bottomNavigationBar is null ? Array.Empty<Widget>() : [bottomNavigationBar],
        ]);
        content = new ColoredBox(backgroundColor ?? s.Surface, content);
        if (floatingActionButton is null) return content;
        return new Stack([content,
            new PositionedDirectional(floatingActionButton, end: 16, bottom: bottomNavigationBar is null ? 16 : 96)], fit: StackFit.Expand, clip: false);
    }
}

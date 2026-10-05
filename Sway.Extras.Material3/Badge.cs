using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Overlays a small status dot, or a count/label, on the top-end corner of its child.</summary>
public sealed class Badge(Widget child, string? label = null, bool isLabelVisible = true, SKColor? backgroundColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        if (!isLabelVisible) return child;
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var bg = backgroundColor ?? s.Error;
        Widget mark = label is null
            ? new SizedBox(6, 6, new DecoratedBox(new BoxDecoration(Color: bg, Shape: BoxShape.Circle)))
            : new ConstrainedBox(new BoxConstraints(16, float.PositiveInfinity, 16, 16),
                new DecoratedBox(new BoxDecoration(Color: bg, BorderRadius: BorderRadius.Circular(8)),
                    new Padding(EdgeInsets.Symmetric(horizontal: 4), new Center(new Text(label,
                        style: theme.TextTheme.LabelSmall.Merge(new TextStyle(Color: s.OnError)))))));
        return new Stack([child, new PositionedDirectional(new IgnorePointer(mark), end: label is null ? 0 : -6, top: label is null ? 0 : -4)],
            clip: false);
    }
}

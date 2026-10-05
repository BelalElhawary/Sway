using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>
/// A column of panels where the owner decides which are open: <paramref name="onExpansionChanged"/> receives the panel index and its
/// new state, and the list is rebuilt with the new <see cref="ExpansionPanel.IsExpanded"/> values.
/// </summary>
public sealed class ExpansionPanelList(IReadOnlyList<ExpansionPanel> panels, Action<int, bool>? onExpansionChanged = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var ms = TimeSpan.FromMilliseconds(200);
        var rows = new List<Widget>();
        for (int i = 0; i < panels.Count; i++)
        {
            int index = i;
            var p = panels[i];
            Widget chevron = new AnimatedRotation(p.IsExpanded ? 0.5f : 0, ms, new Icon(Icons.ExpandMore, 24, s.OnSurfaceVariant));
            Widget headerRow(InteractionState st) => new Container(
                constraints: new BoxConstraints(0, float.PositiveInfinity, 56, float.PositiveInfinity),
                padding: EdgeInsets.Only(left: 24, right: 8), alignment: Alignment.CenterLeft,
                color: p.CanTapOnHeader ? StateLayer.Blend(Colors.Transparent, s.OnSurface, StateLayer.Opacity(st)) : Colors.Transparent,
                child: new Row(children:
                [
                    new Expanded(DefaultTextStyle.Merge(context, theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurface)), p.Header)),
                    p.CanTapOnHeader ? chevron : new IconButton(chevron, () => onExpansionChanged?.Invoke(index, !p.IsExpanded)),
                ]));

            rows.Add(new Material(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                p.CanTapOnHeader
                    ? new Interactive((ctx, st) => headerRow(st), () => onExpansionChanged?.Invoke(index, !p.IsExpanded))
                    : headerRow(default),
                new ClipRect(new AnimatedSize(ms, p.IsExpanded ? p.Body : new SizedBox(height: 0), Alignment.TopCenter, Curves.EaseInOut)),
            ]), p.IsExpanded ? s.SurfaceContainerLow : Colors.Transparent, 0, BorderRadius.Circular(0)));
            if (i < panels.Count - 1) rows.Add(new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)));
        }
        return new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch);
    }
}

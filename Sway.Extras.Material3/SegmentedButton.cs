using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A row of connected toggle segments; one selected at a time, or any number with <c>multiSelectionEnabled</c>.</summary>
public sealed class SegmentedButton<T>(IReadOnlyList<ButtonSegment<T>> segments, IReadOnlyCollection<T> selected,
    Action<IReadOnlyCollection<T>>? onSelectionChanged = null, bool multiSelectionEnabled = false, bool showSelectedIcon = true, Key? key = null)
    : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(20);
        var cells = new List<Widget>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            bool isSelected = selected.Contains(segment.Value, EqualityComparer<T>.Default);
            bool enabled = onSelectionChanged is not null && segment.Enabled;

            void Toggle()
            {
                if (multiSelectionEnabled)
                {
                    var next = selected.Where(v => !EqualityComparer<T>.Default.Equals(v, segment.Value)).ToList();
                    if (!isSelected) next.Add(segment.Value);
                    onSelectionChanged!(next);
                }
                else if (!isSelected) onSelectionChanged!(new[] { segment.Value });
            }

            if (i > 0) cells.Add(new SizedBox(width: 1, height: 40, child: new ColoredBox(s.Outline)));
            cells.Add(new Interactive((ctx, st) =>
            {
                var fg = !enabled ? s.OnSurface.WithOpacity(0.38f) : isSelected ? s.OnSecondaryContainer : s.OnSurface;
                var bg = StateLayer.Blend(isSelected ? s.SecondaryContainer : Colors.Transparent, fg, enabled ? StateLayer.Opacity(st) : 0);
                var leading = isSelected && showSelectedIcon ? Icons.Check : segment.Icon;
                return new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 40, padding: EdgeInsets.Symmetric(horizontal: 12), color: bg,
                    alignment: Alignment.Center, child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                    [
                        ..leading is null ? Array.Empty<Widget>() : [new Icon(leading, 18, fg)],
                        ..segment.Label is null ? Array.Empty<Widget>()
                            : [new Text(segment.Label, style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)))],
                    ]));
            }, enabled ? Toggle : null));
        }

        return new DecoratedBox(new BoxDecoration(BorderRadius: radius, Border: Border.All(s.Outline)),
            new ClipRRect(radius, new IntrinsicHeight(new Row(cells, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch))));
    }
}

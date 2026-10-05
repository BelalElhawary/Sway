using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

/// <summary>Large trees: a lazy list of rich rows, a huge Wrap and a big Grid, to see how scrolling and relayout hold up.</summary>
class StressPage : StatefulWidget
{
    public override State CreateState() => new StressPageState();
}

class StressPageState : State<StressPage>
{
    string _mode = "list";
    readonly HashSet<int> _on = new();

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;

        Widget body = _mode switch
        {
            "wrap" => new SingleChildScrollView(padding: EdgeInsets.All(16), child: new Wrap(spacing: 6, runSpacing: 6,
                children: Enumerable.Range(0, 2000).Select(i => (Widget)new Container(width: 54, height: 28, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: i % 2 == 0 ? s.PrimaryContainer : s.SecondaryContainer, BorderRadius: BorderRadius.Circular(8)),
                    child: new Text($"{i}", style: theme.TextTheme.LabelSmall))).ToList())),
            "grid" => new SingleChildScrollView(padding: EdgeInsets.All(16), child: Grid.AutoFill(70, Enumerable.Range(0, 1200).Select(i => (Widget)new Container(
                height: 40, alignment: Alignment.Center,
                decoration: new BoxDecoration(Color: s.SurfaceContainerHigh, BorderRadius: BorderRadius.Circular(8)),
                child: new Text($"{i}", style: theme.TextTheme.LabelMedium))).ToList(), gap: 6)),
            _ => ListView.Builder(1500, (ctx, i) => new Padding(EdgeInsets.Symmetric(horizontal: 16), new Row(spacing: 16, children:
            [
                new Container(width: 40, height: 40, alignment: Alignment.Center, decoration: new BoxDecoration(Color: s.PrimaryContainer, Shape: BoxShape.Circle),
                    child: new Text($"{i % 100}", style: theme.TextTheme.LabelMedium.Merge(new TextStyle(Color: s.OnPrimaryContainer)))),
                new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                [
                    new Text($"Row {i}", style: theme.TextTheme.BodyLarge),
                    new Text("Secondary text for this row, a little longer than the title", style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), maxLines: 1, overflow: TextOverflow.Ellipsis),
                ])),
                new Switch(_on.Contains(i), v => SetState(() => { if (v) _on.Add(i); else _on.Remove(i); })),
            ])), itemExtent: 64),
        };

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Padding(EdgeInsets.All(16), new Row(spacing: 8, children:
            [
                new Text("Stress", style: theme.TextTheme.HeadlineMedium), new SizedBox(width: 16),
                Mode("list", "Lazy list (1500 rows)"), Mode("wrap", "Wrap (2000 boxes)"), Mode("grid", "Grid (1200 cells)"),
            ])),
            new Expanded(body),
        ]);
    }

    Widget Mode(string id, string label) => new Button(new Text(label), () => SetState(() => _mode = id), _mode == id ? ButtonVariant.Tonal : ButtonVariant.Outlined);
}

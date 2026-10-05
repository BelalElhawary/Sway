using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class LayoutPage : StatelessWidget
{
    static Widget Frame(BuildContext c, Widget child, float? height = null) => new Container(
        height: height, padding: EdgeInsets.All(8),
        decoration: new BoxDecoration(Color: Theme.Of(c).ColorScheme.SurfaceContainerLow, BorderRadius: BorderRadius.Circular(8),
            Border: Border.All(Theme.Of(c).ColorScheme.OutlineVariant)),
        child: child);

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        Widget Chipbox(int i) => Ui.Box($"{i}", i % 2 == 0 ? s.Primary : s.Tertiary, 44, 32 + (i % 3) * 10, i % 2 == 0 ? s.OnPrimary : s.OnTertiary);

        Widget Aligned(string name, MainAxisAlignment a) => new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
        [
            new Text(name, style: Theme.Of(context).TextTheme.LabelSmall),
            Frame(context, new Row(mainAxisAlignment: a, children: [Chipbox(1), Chipbox(2), Chipbox(3)])),
        ]);

        return Ui.Page("Layout", [
            Ui.Section(context, "Row: MainAxisAlignment", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                Aligned("Start", MainAxisAlignment.Start), Aligned("Center", MainAxisAlignment.Center), Aligned("End", MainAxisAlignment.End),
                Aligned("SpaceBetween", MainAxisAlignment.SpaceBetween), Aligned("SpaceAround", MainAxisAlignment.SpaceAround), Aligned("SpaceEvenly", MainAxisAlignment.SpaceEvenly),
            ])),

            Ui.Section(context, "Expanded and Flexible (flex ratios)", Frame(context, new Row(spacing: 8, children:
            [
                new Expanded(Ui.Box("flex 1", s.Primary, h: 48, fg: s.OnPrimary), 1),
                new Expanded(Ui.Box("flex 2", s.Secondary, h: 48, fg: s.OnSecondary), 2),
                new Expanded(Ui.Box("flex 3", s.Tertiary, h: 48, fg: s.OnTertiary), 3),
                Ui.Box("fixed", s.Error, 80, 48, s.OnError),
            ]))),

            Ui.Section(context, "Cross axis and baseline", Frame(context, new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Baseline, children:
            [
                new Text("Small", style: new TextStyle(FontSize: 12)), new Text("Medium", style: new TextStyle(FontSize: 20)), new Text("Large", style: new TextStyle(FontSize: 36)),
                new Text("baseline-aligned", style: new TextStyle(FontSize: 14, Color: s.OnSurfaceVariant)),
            ]))),

            Ui.Section(context, "Wrap", Frame(context, new Wrap(spacing: 8, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.Center,
                children: Enumerable.Range(1, 22).Select(Chipbox).ToList())), "Flows to a new run when the main axis is full."),

            Ui.Section(context, "Grid: columns 1fr / 2fr / 120px, spans and gaps", Frame(context, new Grid(
                columns: [GridTrack.Fr(1), GridTrack.Fr(2), GridTrack.Px(120)], columnGap: 8, rowGap: 8, children:
                [
                    new GridItem(Ui.Box("header (span 3)", s.Primary, fg: s.OnPrimary), column: 0, row: 0, columnSpan: 3),
                    new GridItem(Ui.Box("sidebar", s.Secondary, fg: s.OnSecondary), column: 0, row: 1, rowSpan: 2),
                    Ui.Box("auto 1", s.Tertiary, h: 40, fg: s.OnTertiary), Ui.Box("auto 2", s.Tertiary, h: 40, fg: s.OnTertiary),
                    Ui.Box("auto 3", s.Tertiary, h: 40, fg: s.OnTertiary), Ui.Box("auto 4", s.Tertiary, h: 40, fg: s.OnTertiary),
                    new GridItem(Ui.Box("centred", s.Error, 70, 28, s.OnError), alignment: Alignment.Center),
                ]))),

            Ui.Section(context, "Grid: auto-fill, min column 110px", Frame(context, Grid.AutoFill(110, children:
                Enumerable.Range(1, 11).Select(i => Ui.Box($"cell {i}", i % 3 == 0 ? s.Secondary : s.Primary, h: 44, fg: i % 3 == 0 ? s.OnSecondary : s.OnPrimary)).ToList(),
                gap: 8))),

            Ui.Section(context, "Stack, Positioned and Align", Frame(context, new SizedBox(height: 150, child: new Stack(clip: true, children:
            [
                Positioned.Fill(new ColoredBox(s.SurfaceContainerHighest)),
                new Positioned(Ui.Box("top-left", s.Primary, fg: s.OnPrimary), left: 8, top: 8),
                new Positioned(Ui.Box("bottom-right", s.Secondary, fg: s.OnSecondary), right: 8, bottom: 8),
                new Positioned(Ui.Box("stretched", s.Tertiary, fg: s.OnTertiary), left: 110, top: 50, right: 110),
                new Align(Alignment.TopRight, Ui.Box("align", s.Error, fg: s.OnError)),
                new Align(Alignment.BottomLeft, Transform.Rotate(-0.2f, Ui.Box("rotated", s.InverseSurface, fg: s.OnInverseSurface))),
            ])))),

            Ui.Section(context, "Constraints: ConstrainedBox, FractionallySizedBox, AspectRatio, IntrinsicWidth", new Wrap(spacing: 12, runSpacing: 12, children:
            [
                Frame(context, new ConstrainedBox(new BoxConstraints(100, 140, 40, 60), Ui.Box("min100 max140", s.Primary, fg: s.OnPrimary))),
                Frame(context, new SizedBox(160, 60, new FractionallySizedBox(0.5f, 0.5f, child: Ui.Box("50% x 50%", s.Secondary, fg: s.OnSecondary)))),
                Frame(context, new SizedBox(width: 160, child: new AspectRatio(16 / 9f, Ui.Box("16:9", s.Tertiary, fg: s.OnTertiary)))),
                Frame(context, new IntrinsicWidth(new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    Ui.Box("short", s.Error, fg: s.OnError), Ui.Box("a much longer label", s.InverseSurface, fg: s.OnInverseSurface),
                ]))),
            ])),
        ]);
    }
}

using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

class EffectsPage : StatelessWidget
{
    static Widget Tile(Widget child, float w = 120, float h = 90) => new SizedBox(w, h, child);

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        Widget Label(string t) => new Text(t, style: theme.TextTheme.LabelSmall);

        Widget Swatch() => new Container(width: 110, height: 80, alignment: Alignment.Center,
            decoration: new BoxDecoration(Gradient: new LinearGradient([Colors.Pink, Colors.Orange, Colors.Amber], Alignment.TopLeft, Alignment.BottomRight), BorderRadius: BorderRadius.Circular(12)),
            child: new Text("Aa", style: new TextStyle(Color: Colors.White, FontSize: 28, FontWeight: FontWeight.Bold)));

        Widget Labeled(string label, Widget w) => new Column(mainAxisSize: MainAxisSize.Min, spacing: 6, children: [w, Label(label)]);

        return Ui.Page("Effects", [
            Ui.Section(context, "Elevation levels 0 to 5", new Wrap(spacing: 24, runSpacing: 20, children:
                Enumerable.Range(0, 6).Select(i => Labeled($"level {i}", new Container(width: 88, height: 64, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: s.SurfaceContainerLow, BorderRadius: BorderRadius.Circular(12), BoxShadow: Elevation.Shadows(i, s.Shadow),
                        Border: i == 0 ? Border.All(s.OutlineVariant) : null),
                    child: new Text($"{i}", style: theme.TextTheme.TitleMedium)))).ToList())),

            Ui.Section(context, "Gradients", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                Labeled("linear", Tile(new DecoratedBox(new BoxDecoration(Gradient: new LinearGradient([Colors.Indigo, Colors.Pink], Alignment.TopLeft, Alignment.BottomRight), BorderRadius: BorderRadius.Circular(12))))),
                Labeled("linear with stops", Tile(new DecoratedBox(new BoxDecoration(Gradient: new LinearGradient([Colors.Teal, Colors.White, Colors.Amber], Alignment.CenterLeft, Alignment.CenterRight, [0, 0.3f, 1]), BorderRadius: BorderRadius.Circular(12))))),
                Labeled("radial", Tile(new DecoratedBox(new BoxDecoration(Gradient: new RadialGradient([Colors.Amber, Colors.Red, Colors.Purple], Alignment.Center, 0.6f), BorderRadius: BorderRadius.Circular(12))))),
                Labeled("sweep", Tile(new DecoratedBox(new BoxDecoration(Gradient: new SweepGradient([Colors.Red, Colors.Amber, Colors.Green, Colors.Blue, Colors.Red]), Shape: BoxShape.Circle)), 90, 90)),
            ])),

            Ui.Section(context, "Shape, border and clip", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                Labeled("per-corner radius", Tile(new DecoratedBox(new BoxDecoration(Color: s.PrimaryContainer, BorderRadius: BorderRadius.Only(32, 4, 32, 4))))),
                Labeled("circle + border", Tile(new DecoratedBox(new BoxDecoration(Color: s.SecondaryContainer, Shape: BoxShape.Circle, Border: Border.All(s.Secondary, 4))), 90, 90)),
                Labeled("mixed borders", Tile(new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerHigh,
                    Border: Border.Only(left: new BorderSide(s.Primary, 6), bottom: new BorderSide(s.Error, 3), top: new BorderSide(s.Tertiary, 1)))))),
                Labeled("ClipRRect", Tile(new ClipRRect(BorderRadius.Circular(24), new Container(
                    decoration: new BoxDecoration(Gradient: new LinearGradient([Colors.Blue, Colors.Teal])), alignment: Alignment.BottomCenter,
                    child: new Container(height: 30, color: Colors.White.WithOpacity(0.6f)))))),
                Labeled("opacity 0.4", Tile(new Opacity(0.4f, new DecoratedBox(new BoxDecoration(Color: s.Primary, BorderRadius: BorderRadius.Circular(12)))))),
            ])),

            Ui.Section(context, "Transforms", new Wrap(spacing: 32, runSpacing: 24, children:
            [
                Labeled("rotate 15 deg", Tile(Transform.Rotate(15 * MathF.PI / 180, new DecoratedBox(new BoxDecoration(Color: s.Primary, BorderRadius: BorderRadius.Circular(8)))), 80, 60)),
                Labeled("scale 1.3", Tile(Transform.Scale(1.3f, new DecoratedBox(new BoxDecoration(Color: s.Tertiary, BorderRadius: BorderRadius.Circular(8)))), 70, 50)),
                Labeled("skew", Tile(new Transform(SKMatrix.CreateSkew(-0.4f, 0), new DecoratedBox(new BoxDecoration(Color: s.Secondary, BorderRadius: BorderRadius.Circular(8)))), 80, 60)),
                Labeled("translate", Tile(Transform.Translate(new Offset(12, 6), new DecoratedBox(new BoxDecoration(Color: s.Error, BorderRadius: BorderRadius.Circular(8)))), 80, 60)),
            ]), "Transforms paint and hit-test correctly but do not affect layout."),

            Ui.Section(context, "Filters (ImageFiltered)", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                Labeled("original", Swatch()),
                Labeled("blur 3", new ImageFiltered(Swatch(), blur: 3)),
                Labeled("grayscale", new ImageFiltered(Swatch(), colorFilter: ColorFilters.Grayscale())),
                Labeled("sepia", new ImageFiltered(Swatch(), colorFilter: ColorFilters.Sepia())),
                Labeled("hue-rotate 120", new ImageFiltered(Swatch(), colorFilter: ColorFilters.HueRotate(120))),
                Labeled("invert", new ImageFiltered(Swatch(), colorFilter: ColorFilters.Invert())),
                Labeled("saturate 2.5", new ImageFiltered(Swatch(), colorFilter: ColorFilters.Saturate(2.5f))),
                Labeled("brightness 1.4", new ImageFiltered(Swatch(), colorFilter: ColorFilters.Brightness(1.4f))),
            ])),

            Ui.Section(context, "Text effects", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new Text("Text shadow", style: new TextStyle(FontSize: 30, FontWeight: FontWeight.Bold, Color: s.Primary,
                    Shadows: [new BoxShadow(Colors.Black.WithOpacity(0.35f), new Offset(3, 3), 4)])),
                new Text("Underline, strike-through and letter spacing", style: new TextStyle(FontSize: 16, Decoration: TextDecoration.Underline | TextDecoration.LineThrough, LetterSpacing: 1.5f, DecorationColor: s.Error)),
                new Text(new TextSpan("Rich ", new TextStyle(FontSize: 18), [
                    new TextSpan("bold ", new TextStyle(FontWeight: FontWeight.Bold)), new TextSpan("italic ", new TextStyle(Italic: true)),
                    new TextSpan("coloured", new TextStyle(Color: s.Tertiary, FontWeight: FontWeight.W600)),
                ])),
                new SizedBox(width: 300, child: new Text("A long paragraph that is clamped to two lines with an ellipsis so that you can see overflow handling at work in a narrow box.",
                    maxLines: 2, overflow: TextOverflow.Ellipsis)),
            ])),
        ]);
    }
}

using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class ComponentsPage : StatefulWidget
{
    public override State CreateState() => new ComponentsPageState();
}

class ComponentsPageState : State<ComponentsPage>
{
    int _count;
    readonly HashSet<string> _chips = new() { "Filter" };
    float _progress = 0.35f;

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;

        return Ui.Page("Material 3 components", [
            Ui.Section(context, "Buttons", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Wrap(spacing: 12, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new FilledButton(new Text("Filled"), () => SetState(() => _count++)),
                    new FilledTonalButton(new Text("Tonal"), () => SetState(() => _count++)),
                    new ElevatedButton(new Text("Elevated"), () => SetState(() => _count++)),
                    new OutlinedButton(new Text("Outlined"), () => SetState(() => _count++)),
                    new TextButton(new Text("Text"), () => SetState(() => _count++)),
                    new FilledButton(new Text("With icon"), () => SetState(() => _count++), Icons.Add),
                    new FilledButton(new Text("Disabled"), null),
                    new OutlinedButton(new Text("Disabled"), null),
                ]),
                new Wrap(spacing: 8, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new IconButton(new Icon(Icons.Favorite), () => { }),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Filled),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Tonal),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Outlined),
                    new IconButton(new Icon(Icons.Delete), null),
                    new Text($"  pressed {_count} times", style: theme.TextTheme.BodyMedium),
                ]),
            ])),

            Ui.Section(context, "Chips", new Wrap(spacing: 8, runSpacing: 8, children:
            [
                ..new[] { "Filter", "Search", "Music", "Travel" }.Select(c => (Widget)new Chip(new Text(c),
                    () => SetState(() => { if (!_chips.Remove(c)) _chips.Add(c); }), selected: _chips.Contains(c))),
                new Chip(new Text("Assist"), () => { }, icon: Icons.Info),
                new Chip(new Text("Deletable"), () => { }, onDeleted: () => { }),
            ])),

            Ui.Section(context, "Cards and lists", new Wrap(spacing: 12, runSpacing: 12, children:
            [
                ..new[] { CardVariant.Elevated, CardVariant.Filled, CardVariant.Outlined }.Select(v => (Widget)new SizedBox(width: 250, child:
                    new Card(variant: v, onTap: () => Dialogs.ShowSnackBar(context, $"{v} card tapped"), child: new Padding(EdgeInsets.All(16), new Column(
                        crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                        [
                            new Text(v.ToString(), style: theme.TextTheme.TitleMedium),
                            new Text("Tap me for a snack bar.", style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                        ]))))),
                new SizedBox(width: 340, child: new Card(variant: CardVariant.Outlined, child: new Column(mainAxisSize: MainAxisSize.Min, children:
                [
                    new ListTile(new Text("Inbox"), new Text("12 unread"), new Icon(Icons.Home), new Text("12"), () => { }, selected: true),
                    new Divider(height: 1),
                    new ListTile(new Text("Favourites"), null, new Icon(Icons.Favorite), null, () => { }),
                    new Divider(height: 1),
                    new ListTile(new Text("Settings"), new Text("Account and privacy"), new Icon(Icons.Info), new Icon(Icons.ChevronRight), () => { }),
                ]))),
            ])),

            Ui.Section(context, "Progress", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 14, children:
            [
                new LinearProgressIndicator(_progress),
                new LinearProgressIndicator(),
                new Row(spacing: 16, children:
                [
                    new CircularProgressIndicator(_progress), new CircularProgressIndicator(),
                    new FilledTonalButton(new Text("+10%"), () => SetState(() => _progress = (_progress + 0.1f) % 1.1f)),
                ]),
            ])),

            Ui.Section(context, "Dialogs and snack bars", new Wrap(spacing: 12, runSpacing: 12, children:
            [
                new FilledButton(new Text("Show dialog"), () => Dialogs.Show(context, (ctx, close) => new AlertDialog(
                    icon: Icons.Info, title: new Text("Discard draft?"),
                    content: new Text("Your changes will be lost if you leave now."),
                    actions: [new TextButton(new Text("Cancel"), close), new TextButton(new Text("Discard"), () => { close(); Dialogs.ShowSnackBar(context, "Draft discarded", "Undo"); })]))),
                new OutlinedButton(new Text("Show snack bar"), () => Dialogs.ShowSnackBar(context, "Saved to your device", "Undo", () => { })),
            ])),

            Ui.Section(context, "Type scale", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
            [
                new Text("Display Small", style: theme.TextTheme.DisplaySmall),
                new Text("Headline Medium", style: theme.TextTheme.HeadlineMedium),
                new Text("Title Large", style: theme.TextTheme.TitleLarge),
                new Text("Body Large: the quick brown fox jumps over the lazy dog", style: theme.TextTheme.BodyLarge),
                new Text("Label Large", style: theme.TextTheme.LabelLarge),
            ])),

            Ui.Section(context, "Colour roles", new Wrap(spacing: 8, runSpacing: 8, children:
            [
                Swatch("primary", s.Primary, s.OnPrimary), Swatch("primaryContainer", s.PrimaryContainer, s.OnPrimaryContainer),
                Swatch("secondaryContainer", s.SecondaryContainer, s.OnSecondaryContainer), Swatch("tertiaryContainer", s.TertiaryContainer, s.OnTertiaryContainer),
                Swatch("error", s.Error, s.OnError), Swatch("surfaceContainer", s.SurfaceContainer, s.OnSurface),
                Swatch("surfaceContainerHigh", s.SurfaceContainerHigh, s.OnSurface), Swatch("inverseSurface", s.InverseSurface, s.OnInverseSurface),
            ])),
        ]);
    }

    static Widget Swatch(string name, SKColor bg, SKColor fg) => new Container(width: 150, height: 56, alignment: Alignment.Center, color: bg,
        child: new Text(name, style: new TextStyle(Color: fg, FontSize: 12, FontWeight: FontWeight.W500)));
}

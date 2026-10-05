using SkiaSharp;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>The same content laid out left-to-right and right-to-left, side by side.</summary>
class RtlPage : StatelessWidget
{
    static Widget Content(BuildContext c, bool rtl)
    {
        var theme = Theme.Of(c);
        var s = theme.ColorScheme;
        string Tr(string en, string ar) => rtl ? ar : en;

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 14, children:
        [
            new Text(Tr("Profile", "الملف الشخصي"), style: theme.TextTheme.HeadlineSmall),
            new TextField(decoration: new InputDecoration(LabelText: Tr("Full name", "الاسم الكامل"), HintText: Tr("Type here", "اكتب هنا"), Prefix: new Icon(Icons.Info))),
            new TextField(new TextEditingController(Tr("Hello world", "مرحبا بالعالم")),
                decoration: new InputDecoration(LabelText: Tr("Message (mixed: abc)", "رسالة (مختلطة: abc)"), Filled: true)),
            new Row(spacing: 8, children:
            [
                new FilledButton(new Text(Tr("Save", "حفظ")), () => { }, Icons.Check),
                new OutlinedButton(new Text(Tr("Cancel", "إلغاء")), () => { }),
                new Spacer(),
                new Icon(Icons.ArrowForward),
            ]),
            new Card(variant: CardVariant.Outlined, margin: EdgeInsets.Zero, child: new Column(mainAxisSize: MainAxisSize.Min, children:
            [
                new ListTile(new Text(Tr("Inbox", "الوارد")), new Text(Tr("12 unread messages", "١٢ رسالة غير مقروءة")), new Icon(Icons.Home), new Text("12"), () => { }, selected: true),
                new Divider(height: 1),
                new ListTile(new Text(Tr("Favourites", "المفضلة")), null, new Icon(Icons.Favorite), new Icon(Icons.ChevronRight), () => { }),
            ])),
            new Wrap(spacing: 8, runSpacing: 8, children:
                new[] { Tr("One", "واحد"), Tr("Two", "اثنان"), Tr("Three", "ثلاثة"), Tr("Four", "أربعة") }.Select(t => (Widget)new Chip(new Text(t), () => { })).ToList()),
            new Padding(EdgeInsetsDirectional.Only(start: 24), new Text(Tr("Indented from the start edge (EdgeInsetsDirectional).", "مسافة بادئة من حافة البداية."),
                style: theme.TextTheme.BodyMedium)),
            new SizedBox(height: 70, child: new Stack(clip: true, children:
            [
                Positioned.Fill(new ColoredBox(s.SurfaceContainerHighest)),
                new PositionedDirectional(Ui.Box(Tr("start", "بداية"), s.Primary, fg: s.OnPrimary), start: 8, top: 8),
                new PositionedDirectional(Ui.Box(Tr("end", "نهاية"), s.Tertiary, fg: s.OnTertiary), end: 8, bottom: 8),
            ])),
            new Row(children: [new Checkbox(true, v => { }), new Text(Tr("Remember me", "تذكرني")), new Spacer(), new Switch(true, v => { })]),
        ]);
    }

    public override Widget Build(BuildContext context) => Ui.Page("Right-to-left", [
        new Builder(ctx => new Row(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 20, children:
        [
            new Expanded(new Directionality(TextDirection.Ltr, Ui.Section(ctx, "LTR", Content(ctx, false)))),
            new Expanded(new Directionality(TextDirection.Rtl, Ui.Section(ctx, "RTL", Content(ctx, true)))),
        ])),
    ]);
}

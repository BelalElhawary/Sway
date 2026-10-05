using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>The demo's own strings, loaded through <see cref="Localizations"/> like an app's would be.</summary>
sealed record DemoStrings(string Title, string Profile, string FullName, string TypeHere, string MessageLabel, string MessageText, string Save, string Cancel,
    string Inbox, string Unread, string Favourites, string[] Numbers, string Indented, string Start, string End, string Remember)
{
    public static readonly LocalizationsDelegate<DemoStrings> Delegate = new DemoStringsDelegate();

    public static DemoStrings Of(BuildContext context) => Localizations.Of<DemoStrings>(context)!;

    public static DemoStrings Create(Locale l) => l.Language == "ar"
        ? new("اللغة والاتجاه", "الملف الشخصي", "الاسم الكامل", "اكتب هنا", "رسالة (مختلطة: abc)", "مرحبا بالعالم", "حفظ", "إلغاء", "الوارد", "١٢ رسالة غير مقروءة",
            "المفضلة", ["واحد", "اثنان", "ثلاثة", "أربعة"], "مسافة بادئة من حافة البداية.", "بداية", "نهاية", "تذكرني")
        : new("Language & direction", "Profile", "Full name", "Type here", "Message (mixed: abc)", "Hello world", "Save", "Cancel", "Inbox", "12 unread messages",
            "Favourites", ["One", "Two", "Three", "Four"], "Indented from the start edge (EdgeInsetsDirectional).", "start", "end", "Remember me");
}

sealed class DemoStringsDelegate : LocalizationsDelegate<DemoStrings>
{
    public override bool IsSupported(Locale locale) => locale.Language is "en" or "ar";
    public override DemoStrings Load(Locale locale) => DemoStrings.Create(locale);
}

/// <summary>One form whose text and layout direction follow the app's locale; switch the language in the app bar.</summary>
class RtlPage : StatelessWidget
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var t = DemoStrings.Of(context);

        return Ui.Page(t.Title, [
            new LayoutBuilder((ctx, box) => new ConstrainedBox(new BoxConstraints(0, 560, 0, float.PositiveInfinity), new Column(
                crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 14, children:
            [
                new Text(t.Profile, style: theme.TextTheme.HeadlineSmall),
                new TextField(decoration: new InputDecoration(LabelText: t.FullName, HintText: t.TypeHere, Prefix: new Icon(Icons.Info))),
                new TextField(new TextEditingController(t.MessageText), key: new ValueKey<string>(t.MessageText),
                    decoration: new InputDecoration(LabelText: t.MessageLabel, Filled: true)),
                new Row(spacing: 8, children:
                [
                    new FilledButton(new Text(t.Save), () => { }, Icons.Check),
                    new OutlinedButton(new Text(t.Cancel), () => { }),
                    new Spacer(),
                    new Icon(Icons.ArrowForward),
                ]),
                new Card(variant: CardVariant.Outlined, margin: EdgeInsets.Zero, child: new Column(mainAxisSize: MainAxisSize.Min, children:
                [
                    new ListTile(new Text(t.Inbox), new Text(t.Unread), new Icon(Icons.Home), new Text("12"), () => { }, selected: true),
                    new Divider(height: 1),
                    new ListTile(new Text(t.Favourites), null, new Icon(Icons.Favorite), new Icon(Icons.ChevronRight), () => { }),
                ])),
                new Wrap(spacing: 8, runSpacing: 8, children: t.Numbers.Select(n => (Widget)new Chip(new Text(n), () => { })).ToList()),
                new Padding(EdgeInsetsDirectional.Only(start: 24), new Text(t.Indented, style: theme.TextTheme.BodyMedium)),
                new SizedBox(height: 70, child: new Stack(clip: true, children:
                [
                    Positioned.Fill(new ColoredBox(s.SurfaceContainerHighest)),
                    new PositionedDirectional(Ui.Box(t.Start, s.Primary, fg: s.OnPrimary), start: 8, top: 8),
                    new PositionedDirectional(Ui.Box(t.End, s.Tertiary, fg: s.OnTertiary), end: 8, bottom: 8),
                ])),
                new Row(children: [new Checkbox(true, v => { }), new Text(t.Remember), new Spacer(), new Switch(true, v => { })]),
            ]))),
        ]);
    }
}

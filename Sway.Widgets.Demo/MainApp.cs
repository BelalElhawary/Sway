using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class MainApp(string page = "counter") : StatefulWidget
{
    public string Page => page;
    public override State CreateState() => new MainAppState();
}

class MainAppState : State<MainApp>
{
    string _page = "";

    public override void InitState() => _page = Widget.Page;

    public override Widget Build(BuildContext context)
    {
        Widget body = _page switch
        {
            "forms" => new FormsDemo(),
            "list" => new ListDemo(),
            "motion" => new MotionDemo(),
            _ => new CounterApp(),
        };
        return new ColoredBox(Colors.White, new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Container(color: Colors.FromRgb(0x111827), padding: EdgeInsets.Symmetric(12, 8), child: new Row(spacing: 8, children:
            [
                new Text("Sway", style: new TextStyle(Color: Colors.White, FontWeight: FontWeight.Bold, FontSize: 16)),
                new SizedBox(width: 12),
                Tab("Counter", "counter"), Tab("Forms", "forms"), Tab("List (1500)", "list"), Tab("Motion", "motion"),
            ])),
            new Expanded(body),
        ]));
    }

    Widget Tab(string label, string page) => new Button(new Text(label), () => SetState(() => _page = page),
        _page == page ? ButtonVariant.Elevated : ButtonVariant.Text, color: _page == page ? Palette() : Colors.White, padding: EdgeInsets.Symmetric(12, 6));

    static SKColor Palette() => Colors.FromRgb(0x2563EB);
}

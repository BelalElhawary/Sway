using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class CounterApp : StatefulWidget
{
    public override State CreateState() => new CounterAppState();
}

class CounterAppState : State<CounterApp>
{
    int _count;
    bool _hover;

    public override Widget Build(BuildContext context) =>
        new ColoredBox(Colors.FromRgb(0xF3F4F6),
            new Center(
                new Container(
                    padding: EdgeInsets.All(24),
                    decoration: new BoxDecoration(
                        Color: Colors.White,
                        BorderRadius: BorderRadius.Circular(16),
                        BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(0.15f), new Offset(0, 6), 18)]),
                    child: new Column(
                        mainAxisSize: MainAxisSize.Min,
                        crossAxisAlignment: CrossAxisAlignment.Start,
                        spacing: 12,
                        children:
                        [
                            new Text($"Count: {_count}", style: new TextStyle(FontSize: 28, FontWeight: FontWeight.Bold)),
                            new Text("Hover and press the buttons.", style: new TextStyle(Color: Colors.Grey)),
                            new Row(spacing: 8, mainAxisSize: MainAxisSize.Min, children:
                            [
                                Button("Increment", Colors.Blue, () => SetState(() => _count++)),
                                Button("Reset", Colors.Grey, () => SetState(() => _count = 0)),
                            ]),
                            new Stack(
                                alignment: Alignment.Center,
                                children:
                                [
                                    new SizedBox(240, 36, new DecoratedBox(new BoxDecoration(
                                        Gradient: new LinearGradient([Colors.Pink, Colors.Orange]),
                                        BorderRadius: BorderRadius.Circular(8)))),
                                    new Text("Gradient + Stack", style: new TextStyle(Color: Colors.White, FontWeight: FontWeight.W600)),
                                ]),
                            new Directionality(TextDirection.Rtl, new SizedBox(240, null,
                                new Text("مرحبا بالعالم – RTL text", style: new TextStyle(FontSize: 16)))),
                        ]))));

    static Widget Button(string label, SKColor color, Action onTap) => new _Button(label, color, onTap);
}

class _Button(string label, SKColor color, Action onTap) : StatefulWidget
{
    public string Label => label;
    public SKColor Color => color;
    public Action OnTap => onTap;
    public override State CreateState() => new _ButtonState();
}

class _ButtonState : State<_Button>
{
    bool _hover, _down;

    public override Widget Build(BuildContext context)
    {
        var c = _down ? Widget.Color.WithOpacity(0.7f) : _hover ? Widget.Color.WithOpacity(0.85f) : Widget.Color;
        return new MouseRegion(
            cursor: MouseCursor.Click,
            onEnter: _ => SetState(() => _hover = true),
            onExit: _ => SetState(() => _hover = false),
            child: new GestureDetector(
                onTap: Widget.OnTap,
                onTapDown: _ => SetState(() => _down = true),
                onTapUp: _ => SetState(() => _down = false),
                onTapCancel: () => SetState(() => _down = false),
                child: new Container(
                    padding: EdgeInsets.Symmetric(16, 10),
                    decoration: new BoxDecoration(Color: c, BorderRadius: BorderRadius.Circular(8)),
                    child: new Text(Widget.Label, style: new TextStyle(Color: Colors.White, FontWeight: FontWeight.W600)))));
    }
}

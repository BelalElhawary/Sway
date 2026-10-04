using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class MotionDemo : StatefulWidget
{
    public override State CreateState() => new MotionDemoState();
}

class MotionDemoState : TickerProviderState<MotionDemo>
{
    bool _toggled, _faded, _expanded, _second;
    int _counter;
    double _target = 1;
    AnimationController _spin = null!, _pulse = null!, _stagger = null!;

    public override void InitState()
    {
        _spin = new AnimationController(this, TimeSpan.FromSeconds(2));
        _spin.Repeat();
        _pulse = new AnimationController(this, TimeSpan.FromMilliseconds(900));
        _pulse.Repeat(reverse: true);
        _stagger = new AnimationController(this, TimeSpan.FromMilliseconds(1200));
        _stagger.Repeat(reverse: true);
    }

    public override void Dispose()
    {
        _spin.Dispose(); _pulse.Dispose(); _stagger.Dispose();
        base.Dispose();
    }

    static Widget Card(string title, Widget body) => new Container(
        padding: EdgeInsets.All(16),
        decoration: new BoxDecoration(Color: Colors.White, BorderRadius: BorderRadius.Circular(12), Border: Border.All(Colors.FromRgb(0xE5E7EB))),
        child: new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
        [
            new Text(title, style: new TextStyle(FontWeight: FontWeight.W600, FontSize: 13, Color: Colors.Grey)),
            body,
        ]));

    Widget Curved(string name, Curve curve) => new Row(spacing: 12, children:
    [
        new SizedBox(width: 90, child: new Text(name, style: new TextStyle(FontSize: 12))),
        new Expanded(new LayoutlessTrack(new AnimatedBuilder(_pulse, (_, __) =>
            new Align(new Alignment(-1 + 2 * curve.Transform(_pulse.Value), 0),
                new Container(width: 14, height: 14, decoration: new BoxDecoration(Color: Colors.Indigo, Shape: BoxShape.Circle)))))),
    ]);

    public override Widget Build(BuildContext context)
    {
        var slide = new OffsetTween(new Offset(-0.3f, 0), new Offset(0.3f, 0)).Animate(new CurvedAnimation(_pulse, Curves.EaseInOut));
        var bar = (int i) => new FloatTween(0.15f, 1).Animate(new CurvedAnimation(_stagger, new Interval(i * 0.12f, 0.5f + i * 0.12f, Curves.EaseOutBack)));

        return new SingleChildScrollView(padding: EdgeInsets.All(20), child: new Column(
            crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 14,
            children:
            [
                new Text("Motion", style: new TextStyle(FontSize: 24, FontWeight: FontWeight.Bold)),

                Card("AnimatedContainer: tap the box", new Row(spacing: 16, children:
                [
                    new GestureDetector(onTap: () => SetState(() => _toggled = !_toggled), child: new AnimatedContainer(
                        TimeSpan.FromMilliseconds(500), curve: Curves.EaseInOutCubic,
                        width: _toggled ? 160 : 80, height: _toggled ? 80 : 80,
                        decoration: new BoxDecoration(
                            Color: _toggled ? Colors.Pink : Colors.Blue,
                            BorderRadius: BorderRadius.Circular(_toggled ? 40 : 8),
                            BoxShadow: [new BoxShadow(Colors.Black.WithOpacity(_toggled ? 0.3f : 0.1f), new Offset(0, _toggled ? 10 : 2), _toggled ? 20 : 6)]),
                        alignment: Alignment.Center,
                        child: new Text(_toggled ? "On" : "Off", style: new TextStyle(Color: Colors.White, FontWeight: FontWeight.Bold)))),
                    new Text("size, colour, radius and shadow tween together"),
                ])),

                Card("Explicit animations: Repeat + transitions", new Row(spacing: 24, children:
                [
                    new RotationTransition(_spin, new Container(width: 48, height: 48, color: Colors.Teal)),
                    new ScaleTransition(new FloatTween(0.6f, 1.2f).Animate(_pulse), new Container(width: 48, height: 48,
                        decoration: new BoxDecoration(Color: Colors.Orange, Shape: BoxShape.Circle))),
                    new SizedBox(width: 120, height: 48, child: new SlideTransition(slide,
                        new Container(width: 120, height: 48, decoration: new BoxDecoration(Color: Colors.Purple, BorderRadius: BorderRadius.Circular(8))))),
                    new FadeTransition(_pulse, new Text("fading", style: new TextStyle(FontSize: 18))),
                ])),

                Card("Staggered with Interval + EaseOutBack", new SizedBox(height: 70, child: new Row(
                    crossAxisAlignment: CrossAxisAlignment.End, spacing: 8, children:
                    Enumerable.Range(0, 8).Select(i => (Widget)new AnimatedBuilder(_stagger, (_, __) =>
                        new Container(width: 28, height: 70 * bar(i).Value, decoration: new BoxDecoration(Color: Colors.Indigo.WithOpacity(0.4f + 0.075f * i),
                            BorderRadius: BorderRadius.Only(topLeft: 4, topRight: 4))))).ToList()))),

                Card("Curves", new Column(mainAxisSize: MainAxisSize.Min, spacing: 6, children:
                [
                    Curved("Linear", Curves.Linear), Curved("EaseInOut", Curves.EaseInOut), Curved("BounceOut", Curves.BounceOut),
                    Curved("ElasticOut", Curves.ElasticOut), Curved("EaseOutBack", Curves.EaseOutBack),
                ])),

                Card("Implicit: opacity, switcher, size, cross-fade", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
                [
                    new Row(spacing: 8, mainAxisSize: MainAxisSize.Min, children:
                    [
                        new Button(new Text("Fade"), () => SetState(() => _faded = !_faded)),
                        new Button(new Text("Counter +1"), () => SetState(() => _counter++), ButtonVariant.Outlined),
                        new Button(new Text("Expand"), () => SetState(() => _expanded = !_expanded), ButtonVariant.Outlined),
                        new Button(new Text("Cross-fade"), () => SetState(() => _second = !_second), ButtonVariant.Text),
                    ]),
                    new AnimatedOpacity(_faded ? 0.15f : 1, TimeSpan.FromMilliseconds(400),
                        new Container(width: 200, height: 24, decoration: new BoxDecoration(Color: Colors.Green, BorderRadius: BorderRadius.Circular(6)))),
                    new AnimatedSwitcher(TimeSpan.FromMilliseconds(300),
                        new Text($"Count {_counter}", key: new ValueKey<int>(_counter), style: new TextStyle(FontSize: 28, FontWeight: FontWeight.Bold)),
                        transitionBuilder: (child, anim) => new SlideTransition(
                            new OffsetTween(new Offset(0, 0.5f), Offset.Zero).Animate(anim), new FadeTransition(anim, child))),
                    new AnimatedSize(TimeSpan.FromMilliseconds(350), curve: Curves.EaseInOut, alignment: Alignment.TopLeft, child:
                        new Container(width: 240, height: _expanded ? 120 : 36, color: Colors.Amber.WithOpacity(0.35f),
                            alignment: Alignment.TopLeft, padding: EdgeInsets.All(8), child: new Text(_expanded ? "Expanded\nwith more content" : "Collapsed"))),
                    new AnimatedCrossFade(
                        new Container(width: 240, height: 40, color: Colors.Blue.WithOpacity(0.3f), alignment: Alignment.Center, child: new Text("First")),
                        new Container(width: 240, height: 80, color: Colors.Pink.WithOpacity(0.3f), alignment: Alignment.Center, child: new Text("Second (taller)")),
                        _second ? CrossFadeState.ShowSecond : CrossFadeState.ShowFirst, TimeSpan.FromMilliseconds(400)),
                ])),

                Card("TweenAnimationBuilder", new Row(spacing: 12, children:
                [
                    new Button(new Text("Retarget"), () => SetState(() => _target = _target == 1 ? 0 : 1)),
                    new TweenAnimationBuilder<float>(new FloatTween(0, (float)_target), TimeSpan.FromMilliseconds(700),
                        (ctx, v, _) => new Container(width: 40 + 140 * v, height: 24,
                            decoration: new BoxDecoration(Color: Colors.Teal.WithOpacity(0.3f + 0.7f * v), BorderRadius: BorderRadius.Circular(12))), curve: Curves.EaseOutCubic),
                ])),
            ]));
    }
}

/// <summary>A fixed-height strip that gives its child the full width so an Align can move across it.</summary>
sealed class LayoutlessTrack(Widget child) : StatelessWidget
{
    public override Widget Build(BuildContext context) => new SizedBox(height: 16, child: new ColoredBox(Colors.FromRgb(0xF3F4F6), child));
}

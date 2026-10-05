using SkiaSharp;
using Sway.Widgets;

namespace Sway.Example;

class MotionPage : StatefulWidget
{
    public override State CreateState() => new MotionPageState();
}

class MotionPageState : TickerProviderState<MotionPage>
{
    bool _toggled, _faded, _expanded, _second;
    int _counter;
    float _target = 1;
    bool _elastic;
    AnimationController _spin = null!, _pulse = null!, _stagger = null!, _curve = null!;

    public override void InitState()
    {
        _spin = new AnimationController(this, TimeSpan.FromSeconds(2));
        _spin.Repeat();
        _pulse = new AnimationController(this, TimeSpan.FromMilliseconds(900));
        _pulse.Repeat(reverse: true);
        _stagger = new AnimationController(this, TimeSpan.FromMilliseconds(1200));
        _stagger.Repeat(reverse: true);
        _curve = new AnimationController(this, TimeSpan.FromMilliseconds(4000));
        _curve.Repeat();
    }

    public override void Dispose()
    {
        _spin.Dispose(); _pulse.Dispose(); _stagger.Dispose(); _curve.Dispose();
        base.Dispose();
    }

    // One cycle is out then back; both legs play the curve forward in time (a reversed controller would play it backwards).
    static float CurvePosition(Curve curve, float t) => t < 0.5f ? curve.Transform(t * 2) : 1 - curve.Transform((t - 0.5f) * 2);

    // How far past 0..1 a curve swings (1 for curves that do not overshoot). The track is sized so the swing just reaches its edges.
    static readonly Dictionary<Curve, float> Reach = new();
    static float ReachOf(Curve curve)
    {
        if (Reach.TryGetValue(curve, out var r)) return r;
        r = 1;
        for (int i = 0; i <= 400; i++) r = Math.Max(r, Math.Abs(curve.Transform(i / 400f) - 0.5f) + 0.5f);
        return Reach[curve] = r;
    }

    /// <summary>Track position (0..1 across the full width) for a curve at cycle time <paramref name="t"/>, with its overshoot kept inside the track.</summary>
    static float TrackPosition(Curve curve, float t)
    {
        float reach = ReachOf(curve);
        return (CurvePosition(curve, t) - (1 - reach)) / (2 * reach - 1);
    }

    Widget CurveRow(BuildContext c, string name, Curve curve)
    {
        var s = Theme.Of(c).ColorScheme;
        return new Row(spacing: 12, children:
        [
            new SizedBox(width: 90, child: new Text(name, style: Theme.Of(c).TextTheme.LabelSmall)),
            new Expanded(new SizedBox(height: 16, child: new ColoredBox(s.SurfaceContainerHighest, new AnimatedBuilder(_curve, (_, __) =>
                // The overshoot of back and elastic curves is scaled to fit the track, not clipped, so its shape stays intact.
                new Align(new Alignment(-1 + 2 * TrackPosition(curve, _curve.Value), 0),
                    new Container(width: 14, height: 14, decoration: new BoxDecoration(Color: s.Primary, Shape: BoxShape.Circle)))))))
        ]);
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var slide = new OffsetTween(new Offset(-0.3f, 0), new Offset(0.3f, 0)).Animate(new CurvedAnimation(_pulse, Curves.EaseInOut));
        Animation<float> Bar(int i) => new FloatTween(0.15f, 1).Animate(new CurvedAnimation(_stagger, new Interval(i * 0.12f, 0.5f + i * 0.12f, Curves.EaseOutBack)));

        return Ui.Page("Motion", [
            Ui.Section(context, "AnimatedContainer", new Wrap(spacing: 16, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new GestureDetector(onTap: () => SetState(() => _toggled = !_toggled), child: new AnimatedContainer(
                    TimeSpan.FromMilliseconds(500), curve: Curves.EaseInOutCubic,
                    width: _toggled ? 160 : 80, height: 80,
                    decoration: new BoxDecoration(
                        Color: _toggled ? s.Tertiary : s.Primary, BorderRadius: BorderRadius.Circular(_toggled ? 40 : 12),
                        BoxShadow: Elevation.Shadows(_toggled ? 4 : 1, s.Shadow)),
                    alignment: Alignment.Center,
                    child: new Text(_toggled ? "On" : "Off", style: new TextStyle(Color: _toggled ? s.OnTertiary : s.OnPrimary, FontWeight: FontWeight.Bold)))),
                new Text("Tap the box: size, colour, radius and shadow tween together."),
            ])),

            Ui.Section(context, "Explicit animations: Repeat and transitions", new Wrap(spacing: 24, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new RotationTransition(_spin, new Container(width: 48, height: 48, color: s.Primary)),
                new ScaleTransition(new FloatTween(0.6f, 1.2f).Animate(_pulse), new Container(width: 48, height: 48,
                    decoration: new BoxDecoration(Color: s.Tertiary, Shape: BoxShape.Circle))),
                new SizedBox(width: 160, height: 48, child: new SlideTransition(slide,
                    new Container(width: 120, height: 48, decoration: new BoxDecoration(Color: s.Secondary, BorderRadius: BorderRadius.Circular(12))))),
                new FadeTransition(_pulse, new Text("fading", style: theme.TextTheme.TitleLarge)),
            ])),

            Ui.Section(context, "Staggered: Interval + EaseOutBack", new SizedBox(height: 70, child: new Row(
                crossAxisAlignment: CrossAxisAlignment.End, spacing: 8, children:
                Enumerable.Range(0, 8).Select(i => (Widget)new AnimatedBuilder(Bar(i), (_, __) =>
                    new Container(width: 28, height: 70 * Bar(i).Value, decoration: new BoxDecoration(Color: s.Primary.WithOpacity(0.4f + 0.075f * i),
                        BorderRadius: BorderRadius.Only(topLeft: 4, topRight: 4))))).ToList()))),

            Ui.Section(context, "Curves", new Column(mainAxisSize: MainAxisSize.Min, spacing: 6, children:
            [
                CurveRow(context, "Linear", Curves.Linear), CurveRow(context, "EaseInOut", Curves.EaseInOut), CurveRow(context, "BounceOut", Curves.BounceOut),
                CurveRow(context, "ElasticOut", Curves.ElasticOut), CurveRow(context, "EaseOutBack", Curves.EaseOutBack),
            ])),

            Ui.Section(context, "ElasticOut in action", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 14, children:
            [
                new FilledButton(new Text(_elastic ? "Spring back" : "Spring"), () => SetState(() => _elastic = !_elastic)),
                new SizedBox(height: 56, child: new ColoredBox(s.SurfaceContainerHighest, new AnimatedAlign(
                    new Alignment(_elastic ? 0.6f : -0.6f, 0), TimeSpan.FromMilliseconds(1400), curve: Curves.ElasticOut,
                    child: new Container(width: 40, height: 40, decoration: new BoxDecoration(Color: s.Primary, Shape: BoxShape.Circle))))),
                new Wrap(spacing: 48, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new Padding(EdgeInsets.Only(left: 12), new AnimatedScale(_elastic ? 1.2f : 0.8f, TimeSpan.FromMilliseconds(1400), curve: Curves.ElasticOut,
                        child: new Container(width: 48, height: 48, decoration: new BoxDecoration(Color: s.Tertiary, BorderRadius: BorderRadius.Circular(10))))),
                    new AnimatedRotation(_elastic ? 0.25f : 0, TimeSpan.FromMilliseconds(1400), curve: Curves.ElasticOut,
                        child: new Container(width: 48, height: 48, decoration: new BoxDecoration(Color: s.Secondary, BorderRadius: BorderRadius.Circular(10)))),
                    new Text("Slide, scale and rotation all spring past their target and settle."),
                ]),
            ])),

            Ui.Section(context, "Implicit: opacity, switcher, size, cross-fade", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Wrap(spacing: 8, runSpacing: 8, children:
                [
                    new FilledButton(new Text("Fade"), () => SetState(() => _faded = !_faded)),
                    new FilledTonalButton(new Text("Counter +1"), () => SetState(() => _counter++)),
                    new OutlinedButton(new Text("Expand"), () => SetState(() => _expanded = !_expanded)),
                    new TextButton(new Text("Cross-fade"), () => SetState(() => _second = !_second)),
                ]),
                new AnimatedOpacity(_faded ? 0.15f : 1, TimeSpan.FromMilliseconds(400),
                    new Container(width: 200, height: 24, decoration: new BoxDecoration(Color: s.Primary, BorderRadius: BorderRadius.Circular(12)))),
                new AnimatedSwitcher(TimeSpan.FromMilliseconds(300),
                    new Text($"Count {_counter}", key: new ValueKey<int>(_counter), style: theme.TextTheme.HeadlineMedium),
                    transitionBuilder: (child, anim) => new SlideTransition(
                        new OffsetTween(new Offset(0, 0.5f), Offset.Zero).Animate(anim), new FadeTransition(anim, child))),
                new AnimatedSize(TimeSpan.FromMilliseconds(350), curve: Curves.EaseInOut, alignment: Alignment.TopLeft, child:
                    new Container(width: 240, height: _expanded ? 120 : 40, color: s.SecondaryContainer,
                        alignment: Alignment.TopLeft, padding: EdgeInsets.All(10), child: new Text(_expanded ? "Expanded\nwith more content" : "Collapsed",
                            style: new TextStyle(Color: s.OnSecondaryContainer)))),
                new AnimatedCrossFade(
                    new Container(width: 240, height: 40, color: s.PrimaryContainer, alignment: Alignment.Center, child: new Text("First", style: new TextStyle(Color: s.OnPrimaryContainer))),
                    new Container(width: 240, height: 80, color: s.TertiaryContainer, alignment: Alignment.Center, child: new Text("Second (taller)", style: new TextStyle(Color: s.OnTertiaryContainer))),
                    _second ? CrossFadeState.ShowSecond : CrossFadeState.ShowFirst, TimeSpan.FromMilliseconds(400)),
            ])),

            Ui.Section(context, "TweenAnimationBuilder", new Wrap(spacing: 12, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new FilledButton(new Text("Retarget"), () => SetState(() => _target = _target == 1 ? 0 : 1)),
                new TweenAnimationBuilder<float>(new FloatTween(0, _target), TimeSpan.FromMilliseconds(700),
                    (ctx, v, _) => new Container(width: 40 + 140 * v, height: 24,
                        decoration: new BoxDecoration(Color: s.Primary.WithOpacity(0.3f + 0.7f * v), BorderRadius: BorderRadius.Circular(12))), curve: Curves.EaseOutCubic),
            ])),
        ]);
    }
}

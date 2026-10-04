---
sidebar_position: 7
---

# Animation

## Implicit animations

Implicit widgets animate to new values whenever they are rebuilt with them. Change a property inside `SetState` and
the widget eases from the old value to the new one.

```csharp
new AnimatedContainer(
    TimeSpan.FromMilliseconds(400),
    curve: Curves.EaseInOutCubic,
    width: _wide ? 200 : 100,
    decoration: new BoxDecoration(Color: _on ? s.Primary : s.Tertiary, BorderRadius: BorderRadius.Circular(_on ? 40 : 8)),
    child: child)
```

| Widget | Animates |
| --- | --- |
| `AnimatedContainer` | Size, constraints, padding, margin, alignment, decoration (colour, border, radius, shadows, gradient), transform |
| `AnimatedOpacity`, `AnimatedPadding`, `AnimatedAlign`, `AnimatedPositioned` | The named property |
| `AnimatedScale`, `AnimatedRotation`, `AnimatedSlide` | Scale, turns, fractional offset |
| `AnimatedDefaultTextStyle` | Text style |
| `AnimatedSwitcher` | Cross-fades (or any transition) between an old and new child |
| `AnimatedSize` | Eases the widget's own size when its child changes size |
| `AnimatedCrossFade` | Fades between two children, easing the size |
| `TweenAnimationBuilder<T>` | Any value you can tween |

Retargeting mid-flight restarts from the value currently on screen. The built-in Material controls use these, which is
why buttons and switches ease between states.

## Explicit animations

For loops and choreography, own an `AnimationController` in a state that derives from `TickerProviderState<T>`:

```csharp
class SpinnerState : TickerProviderState<Spinner>
{
    AnimationController _spin = null!;

    public override void InitState()
    {
        _spin = new AnimationController(this, TimeSpan.FromSeconds(2));
        _spin.Repeat();
    }

    public override void Dispose() { _spin.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context) =>
        new RotationTransition(_spin, new Icon(Icons.Star, 48));
}
```

`AnimationController` offers `Forward`, `Reverse`, `AnimateTo`, `Repeat(reverse: true)`, `Toggle`, `Reset` and
`SetValue`, and notifies listeners per frame and on status changes. Wrap it with `CurvedAnimation(controller, curve)`
and map it to values with tweens:

```csharp
var slide = new OffsetTween(new Offset(-0.3f, 0), Offset.Zero)
    .Animate(new CurvedAnimation(_controller, Curves.EaseOutBack));
new SlideTransition(slide, child)
```

Tweens exist for float, colour, offset, size, edge insets, alignment, constraints, border radius, text style,
matrix and decoration (`FloatTween`, `ColorTween`, ...). Use `Interval(begin, end, curve)` to stagger several
animations from one controller, and `TweenSequence<T>` for keyframes.

Transition widgets drive a subtree from an animation: `FadeTransition`, `ScaleTransition`, `RotationTransition`,
`SlideTransition`, `DecoratedBoxTransition`. For anything else use `AnimatedBuilder(animation, builder, child)`, and
pass static subtrees as `child` so they are not rebuilt every frame.

## Curves

`Curves` has `Linear`, `Ease`, `EaseIn`, `EaseOut`, `EaseInOut`, the quad, cubic and expo families, `FastOutSlowIn`,
`EaseInBack`, `EaseOutBack`, `BounceIn/Out/InOut`, `ElasticIn/Out` and `Decelerate`. Build your own with `new Cubic(a, b,
c, d)`.

## Testing animations

The clock can be frozen so screenshots are deterministic: `binding.UseManualClock()` then
`binding.AdvanceClock(TimeSpan.FromMilliseconds(250))`. The demo CLI exposes this as `--advance 250`.

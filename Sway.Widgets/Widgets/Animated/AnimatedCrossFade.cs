using SkiaSharp;

namespace Sway.Widgets;

public sealed class AnimatedCrossFade(Widget firstChild, Widget secondChild, CrossFadeState crossFadeState, TimeSpan duration,
    Curve? curve = null, Key? key = null) : StatefulWidget(key)
{
    internal Widget First => firstChild;
    internal Widget Second => secondChild;
    internal CrossFadeState State => crossFadeState;
    internal TimeSpan Duration => duration;
    internal Curve Curve => curve ?? Curves.Linear;
    public override State CreateState() => new AnimatedCrossFadeState();
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A widget that animates its properties to new values whenever it is rebuilt with them.</summary>
public abstract class ImplicitlyAnimatedWidget(TimeSpan duration, Curve? curve = null, Action? onEnd = null, Key? key = null) : StatefulWidget(key)
{
    public TimeSpan Duration { get; } = duration;
    public Curve Curve { get; } = curve ?? Curves.Linear;
    public Action? OnEnd { get; } = onEnd;
}

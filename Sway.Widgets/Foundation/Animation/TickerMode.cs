using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Turns the tickers below it on or off. A disabled subtree's animations are paused and cost no frames.</summary>
public sealed class TickerMode(bool enabled, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public bool Enabled => enabled;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((TickerMode)old).Enabled != Enabled;

    /// <summary>Whether tickers under <paramref name="context"/> should run; rebuilds the caller when that changes.</summary>
    public static bool Of(BuildContext context) => context.DependOn<TickerMode>()?.Enabled ?? true;
}

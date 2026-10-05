using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Base for states that own animation controllers; tickers stop when the state is disposed.</summary>
public abstract class TickerProviderState<T> : State<T>, ITickerProvider where T : StatefulWidget
{
    readonly List<Ticker> _tickers = new();

    public Ticker CreateTicker(Action<TimeSpan> onTick)
    {
        var t = new Ticker(onTick) { Muted = !TickerMode.Of(Context) };
        _tickers.Add(t);
        return t;
    }

    public override void DidChangeDependencies()
    {
        bool enabled = TickerMode.Of(Context);
        foreach (var t in _tickers) t.Muted = !enabled;
    }

    public override void Dispose()
    {
        foreach (var t in _tickers) t.Stop();
        _tickers.Clear();
    }
}

using SkiaSharp;

namespace Sway.Widgets;

public interface ITickerProvider
{
    Ticker CreateTicker(Action<TimeSpan> onTick);
}

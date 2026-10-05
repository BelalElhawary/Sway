using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Debug overlay that draws the current frame rate in the top-left corner. Frames are only produced when something
/// changed, so the rate reads as "frames drawn in the last second": it is the animation rate while animating and
/// drops to the last burst's count when the UI goes idle.
/// </summary>
public sealed class FpsOverlay
{
    const double WindowSeconds = 1.0;

    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    readonly Queue<double> _frameTimes = new();
    double _lastFrameStart;
    double _lastFrameMs;

    /// <summary>Frames drawn during the last second.</summary>
    public int Fps
    {
        get
        {
            Trim(_clock.Elapsed.TotalSeconds);
            return _frameTimes.Count;
        }
    }

    /// <summary>Marks the start of a frame; pair with <see cref="EndFrame"/>.</summary>
    public void BeginFrame() => _lastFrameStart = _clock.Elapsed.TotalSeconds;

    public void EndFrame()
    {
        double now = _clock.Elapsed.TotalSeconds;
        _lastFrameMs = (now - _lastFrameStart) * 1000;
        _frameTimes.Enqueue(now);
        Trim(now);
    }

    void Trim(double now)
    {
        while (_frameTimes.Count > 0 && now - _frameTimes.Peek() > WindowSeconds) _frameTimes.Dequeue();
    }

    public void Paint(SKCanvas canvas)
    {
        string text = $"{Fps} FPS  ({_lastFrameMs:0.0} ms)";
        using var font = new SKFont(SKTypeface.Default, 13);
        using var textPaint = new SKPaint { Color = SKColors.Lime, IsAntialias = true };
        using var backPaint = new SKPaint { Color = new SKColor(0, 0, 0, 170), IsAntialias = true };

        float width = font.MeasureText(text);
        var box = new SKRect(6, 6, 6 + width + 16, 6 + 24);
        canvas.DrawRoundRect(box, 4, 4, backPaint);
        canvas.DrawText(text, box.Left + 8, box.Top + 17, SKTextAlign.Left, font, textPaint);
    }
}

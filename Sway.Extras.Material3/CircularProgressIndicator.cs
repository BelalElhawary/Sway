using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class CircularProgressIndicator(float? value = null, SKColor? color = null, float strokeWidth = 4, float size = 40, Key? key = null) : StatefulWidget(key)
{
    internal float? Value => value;
    internal SKColor? Color => color;
    internal float Stroke => strokeWidth;
    internal float Diameter => size;
    public override State CreateState() => new CircularProgressState();
}

sealed class CircularProgressState : TickerProviderState<CircularProgressIndicator>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(CircularProgressIndicator old) => Sync();

    void Sync()
    {
        if (Widget.Value is null && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1400));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (Widget.Value is not null && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        return new CustomPaint(new CircularPainter(Widget.Value, _c?.Value ?? 0, Widget.Color ?? s.Primary, Widget.Stroke),
            size: new Size(Widget.Diameter, Widget.Diameter));
    }
}

sealed class CircularPainter(float? value, float t, SKColor color, float stroke) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        var rect = new SKRect(stroke / 2, stroke / 2, size.Width - stroke / 2, size.Height - stroke / 2);
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, StrokeCap = SKStrokeCap.Round };
        if (value is { } v)
        {
            canvas.DrawArc(rect, -90, 360 * Math.Clamp(v, 0, 1), false, p);
            return;
        }
        float rotate = t * 360 * 2;
        float sweep = 30 + 240 * (0.5f - 0.5f * MathF.Cos(t * MathF.PI * 2));
        canvas.DrawArc(rect, -90 + rotate, sweep, false, p);
    }
}

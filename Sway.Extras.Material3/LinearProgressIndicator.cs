using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class LinearProgressIndicator(float? value = null, SKColor? color = null, SKColor? trackColor = null, float height = 4, Key? key = null) : StatefulWidget(key)
{
    internal float? Value => value;
    internal SKColor? Color => color;
    internal SKColor? Track => trackColor;
    internal float Height => height;
    public override State CreateState() => new LinearProgressState();
}

sealed class LinearProgressState : TickerProviderState<LinearProgressIndicator>
{
    AnimationController? _c;

    public override void InitState() => Sync();
    public override void DidUpdateWidget(LinearProgressIndicator old) => Sync();

    void Sync()
    {
        if (Widget.Value is null && _c is null)
        {
            _c = new AnimationController(this, TimeSpan.FromMilliseconds(1800));
            _c.AddListener(() => { if (Mounted) SetState(); });
            _c.Repeat();
        }
        else if (Widget.Value is not null && _c is not null) { _c.Dispose(); _c = null; }
    }

    public override void Dispose() { _c?.Dispose(); base.Dispose(); }

    public override Widget Build(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        return new SizedBox(height: Widget.Height, child: new ClipRRect(BorderRadius.Circular(Widget.Height / 2),
            new CustomPaint(new LinearProgressPainter(Widget.Value, _c?.Value ?? 0, Widget.Color ?? s.Primary, Widget.Track ?? s.SecondaryContainer))));
    }
}

sealed class LinearProgressPainter(float? value, float t, SKColor color, SKColor track) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        using var tp = new SKPaint { Color = track };
        canvas.DrawRect(0, 0, size.Width, size.Height, tp);
        using var p = new SKPaint { Color = color, IsAntialias = true };
        if (value is { } v)
        {
            canvas.DrawRect(0, 0, size.Width * Math.Clamp(v, 0, 1), size.Height, p);
            return;
        }
        // Two bars chase each other: the long one leads, the short one trails.
        float Bar(float phase, float lead, float tail)
        {
            float x = Math.Clamp((t - phase) / (1 - phase), 0, 1);
            float a = Curves.EaseInOut.Transform(Math.Clamp(x / lead, 0, 1));
            float b = Curves.EaseInOut.Transform(Math.Clamp((x - (1 - tail)) / tail, 0, 1));
            canvas.DrawRect(size.Width * (-0.3f + 1.3f * b), 0, size.Width * (1.3f * (a - b) + 0.0001f), size.Height, p);
            return x;
        }
        Bar(0, 0.7f, 0.55f);
        Bar(0.4f, 0.8f, 0.5f);
    }
}

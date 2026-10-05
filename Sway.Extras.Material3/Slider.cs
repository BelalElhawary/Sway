using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>Material 3 slider: drag the thumb, click the track, or use the arrow keys, Home, End and Page Up/Down.</summary>
public sealed class Slider(float value, Action<float>? onChanged = null, float min = 0, float max = 1, int? divisions = null,
    Func<float, string>? label = null, Action<float>? onChangeEnd = null, Key? key = null) : StatefulWidget(key)
{
    internal float Value => value;
    internal Action<float>? OnChanged => onChanged;
    internal float Min => min;
    internal float Max => max;
    internal int? Divisions => divisions;
    internal Func<float, string>? Label => label;
    internal Action<float>? OnChangeEnd => onChangeEnd;
    public override State CreateState() => new SliderState();
}

sealed class SliderState : State<Slider>
{
    const float ThumbRadius = 10, TrackHeight = 4;

    bool _dragging, _hover, _focused;

    // Several input events can arrive before the app rebuilds the slider with the first one's value; keep building on the latest.
    float? _pending;
    float Current => _pending ?? Widget.Value;

    public override void DidUpdateWidget(Slider old) => _pending = null;

    float Fraction => Widget.Max <= Widget.Min ? 0 : Math.Clamp((Current - Widget.Min) / (Widget.Max - Widget.Min), 0, 1);

    float Snap(float fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (Widget.Divisions is { } d and > 0) fraction = MathF.Round(fraction * d) / d;
        return Widget.Min + fraction * (Widget.Max - Widget.Min);
    }

    void Emit(float value)
    {
        value = Math.Clamp(value, Widget.Min, Widget.Max);
        if (value == Current) return;
        _pending = value;
        Widget.OnChanged?.Invoke(value);
    }

    void FromPointer(float globalX)
    {
        if (Widget.OnChanged is null || Context.FindRenderObject() is not RenderBox box) return;
        float left = box.LocalToGlobal(Offset.Zero).Dx;
        float travel = Math.Max(1, box.Size.Width - ThumbRadius * 2);
        float t = (globalX - left - ThumbRadius) / travel;
        if (Directionality.Of(Context) == TextDirection.Rtl) t = 1 - t;
        Emit(Snap(t));
    }

    void SetFlag(Action change) { if (Mounted) SetState(change); }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown || Widget.OnChanged is null) return false;
        float range = Widget.Max - Widget.Min;
        float step = Widget.Divisions is { } d and > 0 ? range / d : range / 20;
        float current = Current;
        bool rtl = Directionality.Of(Context) == TextDirection.Rtl;
        float? target = e.Key switch
        {
            "ArrowRight" => current + (rtl ? -step : step),
            "ArrowLeft" => current + (rtl ? step : -step),
            "ArrowUp" => current + step,
            "ArrowDown" => current - step,
            "PageUp" => current + range / 10,
            "PageDown" => current - range / 10,
            "Home" => Widget.Min,
            "End" => Widget.Max,
            _ => null,
        };
        if (target is not { } v) return false;
        Emit(Snap((v - Widget.Min) / Math.Max(1e-6f, range)));
        Widget.OnChangeEnd?.Invoke(Current);
        return true;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool enabled = Widget.OnChanged is not null;
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        var active = enabled ? s.Primary : s.OnSurface.WithOpacity(0.38f);
        var inactive = enabled ? s.SecondaryContainer : s.OnSurface.WithOpacity(0.12f);
        bool showHalo = enabled && (_dragging || _hover || _focused && WidgetsBinding.Instance.Focus.FocusVisible);

        Widget slider = new LayoutBuilder((ctx, constraints) =>
        {
            float width = constraints.HasBoundedWidth ? constraints.MaxWidth : 200;
            float travel = Math.Max(0, width - ThumbRadius * 2);
            float t = Fraction;
            float thumbCenter = ThumbRadius + (rtl ? 1 - t : t) * travel;
            float trackTop = (40 - TrackHeight) / 2;

            var children = new List<Widget>
            {
                new Positioned(new DecoratedBox(new BoxDecoration(Color: inactive, BorderRadius: BorderRadius.Circular(TrackHeight / 2))),
                    left: ThumbRadius, right: ThumbRadius, top: trackTop, height: TrackHeight),
                rtl
                    ? new Positioned(new DecoratedBox(new BoxDecoration(Color: active, BorderRadius: BorderRadius.Circular(TrackHeight / 2))),
                        left: thumbCenter, right: ThumbRadius, top: trackTop, height: TrackHeight)
                    : new Positioned(new DecoratedBox(new BoxDecoration(Color: active, BorderRadius: BorderRadius.Circular(TrackHeight / 2))),
                        left: ThumbRadius, width: Math.Max(0, thumbCenter - ThumbRadius), top: trackTop, height: TrackHeight),
            };

            if (Widget.Divisions is { } d and > 0)
            {
                for (int i = 0; i <= d; i++)
                {
                    float x = ThumbRadius + (rtl ? 1 - i / (float)d : i / (float)d) * travel;
                    bool passed = i / (float)d <= t;
                    children.Add(new Positioned(new DecoratedBox(new BoxDecoration(
                        Color: (passed ? s.OnPrimary : s.OnSecondaryContainer).WithOpacity(0.38f), Shape: BoxShape.Circle)),
                        left: x - 1, top: 19, width: 2, height: 2));
                }
            }

            children.Add(new Positioned(new IgnorePointer(new AnimatedContainer(TimeSpan.FromMilliseconds(100),
                decoration: new BoxDecoration(Color: showHalo ? active.WithOpacity(_dragging ? 0.16f : 0.10f) : Colors.Transparent, Shape: BoxShape.Circle))),
                left: thumbCenter - 20, top: 0, width: 40, height: 40));
            children.Add(new Positioned(new IgnorePointer(new DecoratedBox(new BoxDecoration(Color: active, Shape: BoxShape.Circle,
                BoxShadow: enabled ? Elevation.Shadows(1, s.Shadow) : null))),
                left: thumbCenter - ThumbRadius, top: 20 - ThumbRadius, width: ThumbRadius * 2, height: ThumbRadius * 2));

            if (_dragging && Widget.Label is { } label)
            {
                children.Add(new Positioned(new IgnorePointer(new OverflowBox(
                    new DecoratedBox(new BoxDecoration(Color: s.InverseSurface, BorderRadius: BorderRadius.Circular(Shapes.Small)),
                        new Padding(EdgeInsets.Symmetric(10, 4), new Text(label(Current),
                            style: theme.TextTheme.LabelMedium.Merge(new TextStyle(Color: s.OnInverseSurface))))),
                    alignment: Alignment.BottomCenter, minWidth: 0, maxWidth: 200, minHeight: 0, maxHeight: 40)),
                    left: thumbCenter - 100, top: -34, width: 200, height: 28));
            }

            return new Stack(children, clip: false);
        });

        return new Focus(
            canRequestFocus: enabled, onKey: OnKey, onFocusChange: f => SetFlag(() => _focused = f),
            child: new MouseRegion(
                onEnter: _ => SetFlag(() => _hover = true), onExit: _ => SetFlag(() => _hover = false),
                cursor: enabled ? MouseCursor.Click : MouseCursor.Default, opaque: false,
                child: new GestureDetector(
                    behavior: HitTestBehavior.Opaque,
                    onTapDown: enabled ? d => { SetFlag(() => _dragging = true); FromPointer(d.GlobalPosition.Dx); } : null,
                    onTapUp: enabled ? _ => { SetFlag(() => _dragging = false); Widget.OnChangeEnd?.Invoke(Current); } : null,
                    onTapCancel: enabled ? () => SetFlag(() => _dragging = false) : null,
                    onHorizontalDragUpdate: enabled ? d => FromPointer(d.GlobalPosition.Dx) : null,
                    onHorizontalDragEnd: enabled ? _ => { SetFlag(() => _dragging = false); Widget.OnChangeEnd?.Invoke(Current); } : null,
                    child: new SizedBox(height: 40, child: slider))));
    }
}

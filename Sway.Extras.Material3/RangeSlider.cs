using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The two ends of a <see cref="RangeSlider"/>; <see cref="Start"/> never exceeds <see cref="End"/>.</summary>
public readonly record struct RangeValues(float Start, float End);

/// <summary>
/// Material 3 range slider: two thumbs on one track. A click or drag moves the nearer thumb; the arrow keys, Home and End move the
/// thumb that was touched last.
/// </summary>
public sealed class RangeSlider(RangeValues values, Action<RangeValues>? onChanged = null, float min = 0, float max = 1, int? divisions = null,
    Func<float, string>? label = null, Action<RangeValues>? onChangeEnd = null, Key? key = null) : StatefulWidget(key)
{
    internal RangeValues Values => values;
    internal Action<RangeValues>? OnChanged => onChanged;
    internal float Min => min;
    internal float Max => max;
    internal int? Divisions => divisions;
    internal Func<float, string>? Label => label;
    internal Action<RangeValues>? OnChangeEnd => onChangeEnd;
    public override State CreateState() => new RangeSliderState();
}

sealed class RangeSliderState : State<RangeSlider>
{
    const float ThumbRadius = 10, TrackHeight = 4;

    bool _dragging, _hover, _focused;
    int _active; // 0 = start thumb, 1 = end thumb

    // Several input events can arrive before the app rebuilds with the first one's value; keep building on the latest.
    RangeValues? _pending;
    RangeValues Current => _pending ?? Widget.Values;

    public override void DidUpdateWidget(RangeSlider old) => _pending = null;

    float Fraction(float v) => Widget.Max <= Widget.Min ? 0 : Math.Clamp((v - Widget.Min) / (Widget.Max - Widget.Min), 0, 1);

    float Snap(float fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (Widget.Divisions is { } d and > 0) fraction = MathF.Round(fraction * d) / d;
        return Widget.Min + fraction * (Widget.Max - Widget.Min);
    }

    void Emit(RangeValues next)
    {
        next = new RangeValues(Math.Clamp(next.Start, Widget.Min, Widget.Max), Math.Clamp(next.End, Widget.Min, Widget.Max));
        if (next == Current) return;
        _pending = next;
        Widget.OnChanged?.Invoke(next);
    }

    void Move(int thumb, float value)
    {
        var c = Current;
        Emit(thumb == 0 ? new RangeValues(Math.Min(value, c.End), c.End) : new RangeValues(c.Start, Math.Max(value, c.Start)));
    }

    float FractionAt(float globalX)
    {
        if (Context.FindRenderObject() is not RenderBox box) return 0;
        float left = box.LocalToGlobal(Offset.Zero).Dx;
        float travel = Math.Max(1, box.Size.Width - ThumbRadius * 2);
        float t = (globalX - left - ThumbRadius) / travel;
        return Directionality.Of(Context) == TextDirection.Rtl ? 1 - t : t;
    }

    void Begin(float globalX)
    {
        if (Widget.OnChanged is null) return;
        float v = Snap(FractionAt(globalX));
        var c = Current;
        // The nearer thumb wins; when both sit together, pick the one that can actually move toward the pointer.
        _active = Math.Abs(v - c.Start) < Math.Abs(v - c.End) || Math.Abs(v - c.Start) == Math.Abs(v - c.End) && v < c.Start ? 0 : 1;
        Move(_active, v);
    }

    void Drag(float globalX)
    {
        if (Widget.OnChanged is not null) Move(_active, Snap(FractionAt(globalX)));
    }

    void SetFlag(Action change) { if (Mounted) SetState(change); }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown || Widget.OnChanged is null) return false;
        float range = Widget.Max - Widget.Min;
        float step = Widget.Divisions is { } d and > 0 ? range / d : range / 20;
        bool rtl = Directionality.Of(Context) == TextDirection.Rtl;
        float current = _active == 0 ? Current.Start : Current.End;
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
        Move(_active, Snap((v - Widget.Min) / Math.Max(1e-6f, range)));
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
            var c = Current;
            float X(float v) => ThumbRadius + (rtl ? 1 - Fraction(v) : Fraction(v)) * travel;
            float xs = X(c.Start), xe = X(c.End);
            float lo = Math.Min(xs, xe), hi = Math.Max(xs, xe);
            float trackTop = (40 - TrackHeight) / 2;

            var children = new List<Widget>
            {
                new Positioned(new DecoratedBox(new BoxDecoration(Color: inactive, BorderRadius: BorderRadius.Circular(TrackHeight / 2))),
                    left: ThumbRadius, right: ThumbRadius, top: trackTop, height: TrackHeight),
                new Positioned(new DecoratedBox(new BoxDecoration(Color: active, BorderRadius: BorderRadius.Circular(TrackHeight / 2))),
                    left: lo, width: hi - lo, top: trackTop, height: TrackHeight),
            };

            if (Widget.Divisions is { } d and > 0)
            {
                for (int i = 0; i <= d; i++)
                {
                    float f = i / (float)d;
                    bool inside = f >= Fraction(c.Start) - 1e-4f && f <= Fraction(c.End) + 1e-4f;
                    children.Add(new Positioned(new DecoratedBox(new BoxDecoration(
                        Color: (inside ? s.OnPrimary : s.OnSecondaryContainer).WithOpacity(0.38f), Shape: BoxShape.Circle)),
                        left: ThumbRadius + (rtl ? 1 - f : f) * travel - 1, top: 19, width: 2, height: 2));
                }
            }

            foreach (int thumb in new[] { 0, 1 })
            {
                float x = thumb == 0 ? xs : xe;
                bool halo = showHalo && (_dragging ? _active == thumb : _active == thumb || !_hover);
                children.Add(new Positioned(new IgnorePointer(new AnimatedContainer(TimeSpan.FromMilliseconds(100),
                    decoration: new BoxDecoration(Color: halo ? active.WithOpacity(_dragging ? 0.16f : 0.10f) : Colors.Transparent, Shape: BoxShape.Circle))),
                    left: x - 20, top: 0, width: 40, height: 40));
                children.Add(new Positioned(new IgnorePointer(new DecoratedBox(new BoxDecoration(Color: active, Shape: BoxShape.Circle,
                    BoxShadow: enabled ? Elevation.Shadows(1, s.Shadow) : null))),
                    left: x - ThumbRadius, top: 20 - ThumbRadius, width: ThumbRadius * 2, height: ThumbRadius * 2));
            }

            if (_dragging && Widget.Label is { } label)
            {
                float x = _active == 0 ? xs : xe;
                children.Add(new Positioned(new IgnorePointer(new OverflowBox(
                    new DecoratedBox(new BoxDecoration(Color: s.InverseSurface, BorderRadius: BorderRadius.Circular(Shapes.Small)),
                        new Padding(EdgeInsets.Symmetric(10, 4), new Text(label(_active == 0 ? c.Start : c.End),
                            style: theme.TextTheme.LabelMedium.Merge(new TextStyle(Color: s.OnInverseSurface))))),
                    alignment: Alignment.BottomCenter, minWidth: 0, maxWidth: 200, minHeight: 0, maxHeight: 40)),
                    left: x - 100, top: -34, width: 200, height: 28));
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
                    onTapDown: enabled ? d => { SetFlag(() => _dragging = true); Begin(d.GlobalPosition.Dx); } : null,
                    onTapUp: enabled ? _ => { SetFlag(() => _dragging = false); Widget.OnChangeEnd?.Invoke(Current); } : null,
                    onTapCancel: enabled ? () => SetFlag(() => _dragging = false) : null,
                    onHorizontalDragUpdate: enabled ? d => Drag(d.GlobalPosition.Dx) : null,
                    onHorizontalDragEnd: enabled ? _ => { SetFlag(() => _dragging = false); Widget.OnChangeEnd?.Invoke(Current); } : null,
                    child: new SizedBox(height: 40, child: slider))));
    }
}

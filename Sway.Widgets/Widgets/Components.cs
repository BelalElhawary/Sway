using SkiaSharp;

namespace Sway.Widgets;

// ---- Slider ----

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

// ---- Badge ----

/// <summary>Overlays a small status dot, or a count/label, on the top-end corner of its child.</summary>
public sealed class Badge(Widget child, string? label = null, bool isLabelVisible = true, SKColor? backgroundColor = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        if (!isLabelVisible) return child;
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var bg = backgroundColor ?? s.Error;
        Widget mark = label is null
            ? new SizedBox(6, 6, new DecoratedBox(new BoxDecoration(Color: bg, Shape: BoxShape.Circle)))
            : new ConstrainedBox(new BoxConstraints(16, float.PositiveInfinity, 16, 16),
                new DecoratedBox(new BoxDecoration(Color: bg, BorderRadius: BorderRadius.Circular(8)),
                    new Padding(EdgeInsets.Symmetric(horizontal: 4), new Center(new Text(label,
                        style: theme.TextTheme.LabelSmall.Merge(new TextStyle(Color: s.OnError)))))));
        return new Stack([child, new PositionedDirectional(new IgnorePointer(mark), end: label is null ? 0 : -6, top: label is null ? 0 : -4)],
            clip: false);
    }
}

// ---- Segmented button ----

public sealed record ButtonSegment<T>(T Value, string? Label = null, IconData? Icon = null, bool Enabled = true);

/// <summary>A row of connected toggle segments; one selected at a time, or any number with <c>multiSelectionEnabled</c>.</summary>
public sealed class SegmentedButton<T>(IReadOnlyList<ButtonSegment<T>> segments, IReadOnlyCollection<T> selected,
    Action<IReadOnlyCollection<T>>? onSelectionChanged = null, bool multiSelectionEnabled = false, bool showSelectedIcon = true, Key? key = null)
    : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var radius = BorderRadius.Circular(20);
        var cells = new List<Widget>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            bool isSelected = selected.Contains(segment.Value, EqualityComparer<T>.Default);
            bool enabled = onSelectionChanged is not null && segment.Enabled;

            void Toggle()
            {
                if (multiSelectionEnabled)
                {
                    var next = selected.Where(v => !EqualityComparer<T>.Default.Equals(v, segment.Value)).ToList();
                    if (!isSelected) next.Add(segment.Value);
                    onSelectionChanged!(next);
                }
                else if (!isSelected) onSelectionChanged!(new[] { segment.Value });
            }

            if (i > 0) cells.Add(new SizedBox(width: 1, height: 40, child: new ColoredBox(s.Outline)));
            cells.Add(new Interactive((ctx, st) =>
            {
                var fg = !enabled ? s.OnSurface.WithOpacity(0.38f) : isSelected ? s.OnSecondaryContainer : s.OnSurface;
                var bg = StateLayer.Blend(isSelected ? s.SecondaryContainer : Colors.Transparent, fg, enabled ? StateLayer.Opacity(st) : 0);
                var leading = isSelected && showSelectedIcon ? Icons.Check : segment.Icon;
                return new AnimatedContainer(TimeSpan.FromMilliseconds(120), height: 40, padding: EdgeInsets.Symmetric(horizontal: 12), color: bg,
                    alignment: Alignment.Center, child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                    [
                        ..leading is null ? Array.Empty<Widget>() : [new Icon(leading, 18, fg)],
                        ..segment.Label is null ? Array.Empty<Widget>()
                            : [new Text(segment.Label, style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: fg)))],
                    ]));
            }, enabled ? Toggle : null));
        }

        return new DecoratedBox(new BoxDecoration(BorderRadius: radius, Border: Border.All(s.Outline)),
            new ClipRRect(radius, new IntrinsicHeight(new Row(cells, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch))));
    }
}

// ---- Tabs ----

public sealed record Tab(string? Text = null, IconData? Icon = null);

/// <summary>Primary tabs: equal-width tabs, or scrollable ones sized to their content, with an underline for the selected tab.</summary>
public sealed class TabBar(IReadOnlyList<Tab> tabs, int selectedIndex, Action<int>? onTap = null, bool isScrollable = false, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool both = tabs.Any(t => t.Icon is not null && t.Text is not null);
        float height = both ? 64 : 48;

        Widget TabCell(int index)
        {
            var tab = tabs[index];
            bool selected = index == selectedIndex;
            return new Interactive((ctx, st) =>
            {
                var fg = selected ? s.Primary : s.OnSurfaceVariant;
                var bg = StateLayer.Blend(Colors.Transparent, selected ? s.Primary : s.OnSurface, StateLayer.Opacity(st));
                return new Container(height: height, color: bg, padding: EdgeInsets.Symmetric(horizontal: isScrollable ? 16 : 8),
                    child: new Column(mainAxisAlignment: MainAxisAlignment.End, children:
                    [
                        new Expanded(new Center(new Column(mainAxisSize: MainAxisSize.Min, spacing: 2, children:
                        [
                            ..tab.Icon is null ? Array.Empty<Widget>() : [new Icon(tab.Icon, 24, fg)],
                            ..tab.Text is null ? Array.Empty<Widget>()
                                : [new Text(tab.Text, style: theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: fg)))],
                        ]))),
                        new AnimatedContainer(TimeSpan.FromMilliseconds(200), height: selected ? 3 : 0, margin: EdgeInsets.Symmetric(horizontal: 4),
                            decoration: new BoxDecoration(Color: s.Primary, BorderRadius: new BorderRadius(new Radius(3, 3), new Radius(3, 3), new Radius(0, 0), new Radius(0, 0)))),
                    ]));
            }, onTap is null ? null : () => onTap(index));
        }

        var cells = Enumerable.Range(0, tabs.Count).Select(TabCell).ToList();
        Widget row = isScrollable
            ? new SingleChildScrollView(new Row(cells, mainAxisSize: MainAxisSize.Min), Axis.Horizontal)
            : new Row(cells.Select(c => (Widget)new Expanded(c)).ToList(), crossAxisAlignment: CrossAxisAlignment.Stretch);
        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            row,
            new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)),
        ]);
    }
}

/// <summary>Shows the child for the selected tab, cross-fading when it changes.</summary>
public sealed class TabBarView(int selectedIndex, IReadOnlyList<Widget> children, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new AnimatedSwitcher(TimeSpan.FromMilliseconds(220), new KeyedSubtree(new ValueKey<int>(selectedIndex), children[selectedIndex]));
}

// ---- Tooltip ----

/// <summary>Shows a short label near its child after the pointer rests on it for <c>waitDuration</c>.</summary>
public sealed class Tooltip(string message, Widget child, TimeSpan? waitDuration = null, Key? key = null) : StatefulWidget(key)
{
    internal string Message => message;
    internal Widget Child => child;
    internal TimeSpan Wait => waitDuration ?? TimeSpan.FromMilliseconds(500);
    public override State CreateState() => new TooltipState();
}

sealed class TooltipState : State<Tooltip>
{
    OverlayEntry? _entry;
    Action? _showTimer;

    public override void Dispose() => Hide();

    void ScheduleShow()
    {
        Hide();
        _showTimer = Show;
        WidgetsBinding.Instance.ScheduleTimer(Widget.Wait, _showTimer);
    }

    void Show()
    {
        _showTimer = null;
        if (_entry is not null || !Mounted || Context.FindRenderObject() is not RenderBox box || box.SizeOrNull is not { } size) return;
        var overlay = Overlay.MaybeOf(Context);
        if (overlay is null) return;

        var origin = box.LocalToGlobal(Offset.Zero);
        var window = WidgetsBinding.Instance.RenderView.WindowSize;
        bool above = origin.Dy + size.Height + 40 > window.Height && origin.Dy > 40;
        var owner = Context;
        string message = Widget.Message;

        _entry = new OverlayEntry(ctx =>
        {
            var theme = Theme.Of(owner);
            var s = theme.ColorScheme;
            Widget bubble = new DecoratedBox(new BoxDecoration(Color: s.InverseSurface, BorderRadius: BorderRadius.Circular(Shapes.ExtraSmall)),
                new Padding(EdgeInsets.Symmetric(8, 4), new Text(message, style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnInverseSurface)))));
            return Dialogs.Wrap(owner, new Stack([
                new Positioned(new IgnorePointer(new OverflowBox(bubble, alignment: above ? Alignment.BottomCenter : Alignment.TopCenter,
                    minWidth: 0, maxWidth: 320, minHeight: 0, maxHeight: 200)),
                    left: origin.Dx, width: size.Width,
                    top: above ? origin.Dy - 8 - 200 : origin.Dy + size.Height + 8, height: 200),
            ], fit: StackFit.Expand, clip: false));
        });
        overlay.Insert(_entry);
    }

    void Hide()
    {
        if (_showTimer is not null) WidgetsBinding.Instance.CancelTimer(_showTimer);
        _showTimer = null;
        _entry?.Remove();
        _entry = null;
    }

    public override Widget Build(BuildContext context) =>
        new MouseRegion(onEnter: _ => ScheduleShow(), onExit: _ => Hide(), opaque: false,
            child: new Listener(Widget.Child, onPointerDown: _ => Hide(), behavior: HitTestBehavior.Translucent));
}

// ---- Search bar and selectable text ----

/// <summary>A Material 3 search bar: a rounded surface with a search icon, the text input and an optional trailing widget.</summary>
public sealed class SearchBar(TextEditingController? controller = null, string hintText = "Search", Action<string>? onChanged = null,
    Action<string>? onSubmitted = null, Widget? trailing = null, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal string Hint => hintText;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal Widget? Trailing => trailing;
    public override State CreateState() => new SearchBarState();
}

sealed class SearchBarState : State<SearchBar>
{
    TextEditingController? _owned;
    TextEditingController Controller => Widget.Controller ?? (_owned ??= new TextEditingController());

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        return new DecoratedBox(new BoxDecoration(Color: s.SurfaceContainerHigh, BorderRadius: BorderRadius.Circular(28),
                BoxShadow: Elevation.Shadows(1, s.Shadow)),
            new ConstrainedBox(new BoxConstraints(360, 720, 56, 56), new Padding(EdgeInsets.Symmetric(horizontal: 16),
                new Row(spacing: 16, children:
                [
                    new Icon(Icons.Search, 24, s.OnSurface),
                    new Expanded(new EditableText(Controller, style: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurface)),
                        hintStyle: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant)), hintText: Widget.Hint,
                        onChanged: Widget.OnChanged, onSubmitted: Widget.OnSubmitted)),
                    ..Widget.Trailing is null ? Array.Empty<Widget>() : [Widget.Trailing],
                ]))));
    }
}

/// <summary>Read-only text the user can select with the mouse or keyboard and copy.</summary>
public sealed class SelectableText(string text, TextStyle? style = null, Key? key = null) : StatefulWidget(key)
{
    internal string Text => text;
    internal TextStyle? Style => style;
    public override State CreateState() => new SelectableTextState();
}

sealed class SelectableTextState : State<SelectableText>
{
    readonly TextEditingController _controller = new();

    public override void InitState() => _controller.Text = Widget.Text;

    public override void DidUpdateWidget(SelectableText old)
    {
        if (old.Text != Widget.Text) _controller.Text = Widget.Text;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var style = theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: theme.ColorScheme.OnSurface)).Merge(Widget.Style ?? new TextStyle());
        return new EditableText(_controller, style: style, maxLines: null, readOnly: true);
    }
}

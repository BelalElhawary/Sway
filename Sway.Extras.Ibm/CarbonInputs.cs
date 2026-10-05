using System.Globalization;
using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>A Carbon text area: a multi-line filled field that is <paramref name="rows"/> lines tall. <paramref name="maxCount"/> shows a character counter.</summary>
public sealed class CarbonTextArea(TextEditingController? controller = null, FocusNode? focusNode = null, string? label = null, string? placeholder = null,
    string? helperText = null, string? errorText = null, int rows = 4, int? maxCount = null, bool enabled = true, bool onLayer = false,
    Action<string>? onChanged = null, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal string? HelperText => helperText;
    internal string? ErrorText => errorText;
    internal int Rows => rows;
    internal int? MaxCount => maxCount;
    internal bool Enabled => enabled;
    internal bool OnLayer => onLayer;
    internal Action<string>? OnChanged => onChanged;
    public override State CreateState() => new CarbonTextAreaState();
}

sealed class CarbonTextAreaState : State<CarbonTextArea>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;
    bool _hover;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "CarbonTextArea" });

    public override void InitState()
    {
        Node.Changed += OnChange;
        Controller.Changed += OnChange;
    }

    void OnChange() { if (Mounted) SetState(); }

    public override void Dispose()
    {
        Node.Changed -= OnChange;
        Controller.Changed -= OnChange;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.Enabled;
        int count = Controller.Text.Length;
        bool over = Widget.MaxCount is { } m && count > m;
        bool error = Widget.ErrorText is not null || over;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled));
        float height = Widget.Rows * 20 + 24;

        Widget field = new ClipRect(new EditableText(Controller, Node, text, text.Merge(new TextStyle(Color: c.TextPlaceholder)), Widget.Placeholder,
            false, null, Widget.Rows, !enabled, c.TextPrimary, c.Highlight, Widget.OnChanged));
        Widget frame = CarbonField.Frame(theme, height, Node.HasFocus, _hover && enabled, error, enabled, Widget.OnLayer ? c.Field02 : null,
            new GestureDetector(onTap: enabled ? () => Node.RequestFocus() : null, behavior: HitTestBehavior.Opaque,
                child: new Padding(EdgeInsets.Symmetric(horizontal: 16, vertical: 12), field)));

        var below = Widget.ErrorText ?? (over ? CarbonLocalizations.Of(context).TooManyCharacters : null);
        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                ..Widget.Label is null && Widget.MaxCount is null ? Array.Empty<Widget>() :
                [new Row(children:
                [
                    new Expanded(Widget.Label is null ? new SizedBox() : new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))),
                    ..Widget.MaxCount is { } max ? [new Text($"{count}/{max}", style: theme.Type.Label01.Merge(new TextStyle(Color: over ? c.TextError : c.TextSecondary)))] : Array.Empty<Widget>(),
                ])],
                frame,
                ..below is not null ? [new Text(below, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextError)))]
                    : Widget.HelperText is { } h ? [new Text(h, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextHelper)))]
                    : Array.Empty<Widget>(),
            ]));
    }
}

/// <summary>
/// A Carbon number input: a field with decrement and increment buttons. The text may be typed freely; it is parsed, clamped to
/// <paramref name="min"/> and <paramref name="max"/> and reported through <c>onChanged</c> when it is a valid number, and tidied when focus leaves.
/// </summary>
public sealed class CarbonNumberInput(double value, Action<double>? onChanged = null, double min = double.NegativeInfinity, double max = double.PositiveInfinity,
    double step = 1, string? label = null, string? helperText = null, string? errorText = null, CarbonFieldSize size = CarbonFieldSize.Medium,
    bool onLayer = false, Key? key = null) : StatefulWidget(key)
{
    internal double Value => value;
    internal Action<double>? OnChanged => onChanged;
    internal double Min => min;
    internal double Max => max;
    internal double Step => step;
    internal string? Label => label;
    internal string? HelperText => helperText;
    internal string? ErrorText => errorText;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    public override State CreateState() => new CarbonNumberInputState();
}

sealed class CarbonNumberInputState : State<CarbonNumberInput>
{
    readonly TextEditingController _text = new();
    readonly FocusNode _node = new() { DebugLabel = "CarbonNumberInput" };
    bool _hover;
    // The newest value this field has emitted, so a rebuild with the parent's older value does not clobber typing.
    double _current;

    static string Format(double v) => v.ToString("0.##########", CultureInfo.CurrentCulture);

    public override void InitState()
    {
        _current = Widget.Value;
        _text.Text = Format(_current);
        _node.Changed += OnFocus;
    }

    public override void DidUpdateWidget(CarbonNumberInput old)
    {
        if (Widget.Value != old.Value && Widget.Value != _current) { _current = Widget.Value; _text.Text = Format(_current); }
    }

    public override void Dispose() => _node.Changed -= OnFocus;

    void OnFocus()
    {
        if (!Mounted) return;
        if (!_node.HasFocus) _text.Text = Format(_current);
        SetState();
    }

    double Clamp(double v) => Math.Clamp(v, Widget.Min, Widget.Max);

    void Set(double v)
    {
        v = Clamp(v);
        _current = v;
        _text.Text = Format(v);
        Widget.OnChanged?.Invoke(v);
    }

    void Typed(string s)
    {
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var v))
        {
            _current = Clamp(v);
            Widget.OnChanged?.Invoke(_current);
        }
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.OnChanged is not null;
        float height = (float)Widget.Size;
        bool invalid = !double.TryParse(_text.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) || parsed < Widget.Min || parsed > Widget.Max;
        bool error = Widget.ErrorText is not null || invalid;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled));

        Widget Step(IconData icon, double delta, bool can) => new Interactive((ctx, st) => new Container(width: height, height: height,
            color: st.Hover && can ? c.FieldHover01 : Colors.Transparent, child: CarbonFocus.Around(st.FocusVisible, theme,
                new Center(new Icon(icon, 16, can ? c.IconPrimary : c.IconDisabled)))), can ? () => Set(_current + delta) : null, focusable: can);

        Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Expanded(new GestureDetector(onTap: enabled ? () => _node.RequestFocus() : null, behavior: HitTestBehavior.Opaque,
                child: new Padding(EdgeInsets.Symmetric(horizontal: 16), new Align(AlignmentDirectional.CenterStart,
                    new EditableText(_text, _node, text, text, null, false, 1, null, !enabled, c.TextPrimary, c.Highlight, Typed,
                        _ => Set(_current)))))),
            new Container(width: 1, height: height / 2, color: c.BorderSubtle01),
            Step(Icons.Remove, -Widget.Step, enabled && _current > Widget.Min),
            new Container(width: 1, height: height / 2, color: c.BorderSubtle01),
            Step(Icons.Add, Widget.Step, enabled && _current < Widget.Max),
        ]);
        var below = Widget.ErrorText ?? (invalid ? CarbonLocalizations.Of(context).EnterValidNumber : null);
        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                ..Widget.Label is null ? Array.Empty<Widget>() : [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
                CarbonField.Frame(theme, height, _node.HasFocus, _hover && enabled, error, enabled, Widget.OnLayer ? c.Field02 : null, content),
                ..below is not null ? [new Text(below, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextError)))]
                    : Widget.HelperText is { } h ? [new Text(h, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextHelper)))]
                    : Array.Empty<Widget>(),
            ]));
    }
}

/// <summary>A Carbon slider: a thin track with a round thumb. Drag, click the track, or use the arrow, Home and End keys.</summary>
public sealed class CarbonSlider(double value, Action<double>? onChanged = null, double min = 0, double max = 100, double step = 1, string? label = null,
    bool showValue = true, Key? key = null) : StatefulWidget(key)
{
    internal double Value => value;
    internal Action<double>? OnChanged => onChanged;
    internal double Min => min;
    internal double Max => max;
    internal double Step => step;
    internal string? Label => label;
    internal bool ShowValue => showValue;
    public override State CreateState() => new CarbonSliderState();
}

sealed class CarbonSliderState : State<CarbonSlider>
{
    const float Thumb = 14;

    // Pointer events can arrive faster than the owner rebuilds with the first one's value.
    double? _pending;
    double Current => _pending ?? Widget.Value;
    float _width = 1;
    readonly FocusNode _node = new() { DebugLabel = "CarbonSlider" };

    public override void DidUpdateWidget(CarbonSlider old) => _pending = null;

    double Snap(double v)
    {
        if (Widget.Step > 0) v = Widget.Min + Math.Round((v - Widget.Min) / Widget.Step) * Widget.Step;
        return Math.Clamp(v, Widget.Min, Widget.Max);
    }

    void Emit(double v)
    {
        v = Snap(v);
        if (v == Current) return;
        _pending = v;
        Widget.OnChanged?.Invoke(v);
        if (Mounted) SetState();
    }

    void FromX(float x)
    {
        if (Widget.OnChanged is null) return;
        float t = Math.Clamp((x - Thumb / 2) / Math.Max(1, _width - Thumb), 0, 1);
        Emit(Widget.Min + t * (Widget.Max - Widget.Min));
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown || Widget.OnChanged is null) return false;
        double step = Widget.Step > 0 ? Widget.Step : (Widget.Max - Widget.Min) / 100;
        double? target = e.Key switch
        {
            "ArrowRight" or "ArrowUp" => Current + step,
            "ArrowLeft" or "ArrowDown" => Current - step,
            "PageUp" => Current + step * 10,
            "PageDown" => Current - step * 10,
            "Home" => Widget.Min,
            "End" => Widget.Max,
            _ => null,
        };
        if (target is null) return false;
        Emit(target.Value);
        return true;
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.OnChanged is not null;
        double range = Math.Max(1e-9, Widget.Max - Widget.Min);
        float frac = (float)Math.Clamp((Current - Widget.Min) / range, 0, 1);

        Widget track = new LayoutBuilder((ctx, box) =>
        {
            _width = box.MaxWidth;
            float x = Thumb / 2 + frac * (box.MaxWidth - Thumb);
            return new SizedBox(height: 24, child: new Stack(
            [
                new Positioned(new Container(height: 2, color: enabled ? c.BorderSubtle01 : c.BorderDisabled), left: Thumb / 2, right: Thumb / 2, top: 11),
                new Positioned(new Container(height: 2, color: enabled ? c.IconPrimary : c.IconDisabled), left: Thumb / 2, width: Math.Max(0, x - Thumb / 2), top: 11),
                new Positioned(new Container(width: Thumb, height: Thumb, decoration: new BoxDecoration(
                    Color: enabled ? c.IconPrimary : c.IconDisabled, BorderRadius: BorderRadius.Circular(Thumb / 2))), left: x - Thumb / 2, top: 5),
            ], clip: false));
        });

        Widget body = new Focus(focusNode: _node, onKey: OnKey, canRequestFocus: enabled, child: new GestureDetector(behavior: HitTestBehavior.Opaque,
            onTapDown: d => { _node.RequestFocus(); FromX(d.LocalPosition.Dx); }, onHorizontalDragUpdate: d => FromX(d.LocalPosition.Dx), child: track));

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
        [
            ..Widget.Label is null ? Array.Empty<Widget>() : [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
            new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 16, children:
            [
                new Text(Widget.Min.ToString("0.##"), style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary))),
                new Expanded(body),
                new Text(Widget.Max.ToString("0.##"), style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary))),
                ..Widget.ShowValue ? [new Container(width: 64, height: 32, color: c.Field01, alignment: Alignment.Center,
                    child: new Text(Current.ToString("0.##"), style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary))))] : Array.Empty<Widget>(),
            ]),
        ]);
    }
}

/// <summary>A Carbon select: a labelled single-choice field. It uses the same menu as <see cref="CarbonDropdown{T}"/>.</summary>
public sealed class CarbonSelect<T>(IReadOnlyList<CarbonDropdownItem<T>> items, T? value = default, Action<T>? onChanged = null, string? label = null,
    string? helperText = null, string? placeholder = null, CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false, Key? key = null)
    : StatelessWidget(key) where T : notnull
{
    public override Widget Build(BuildContext context)
    {
        var c = CarbonTheme.Of(context);
        Widget dropdown = new CarbonDropdown<T>(items, value, onChanged, label, placeholder ?? CarbonLocalizations.Of(context).ChooseAnOption, size, onLayer);
        return helperText is null ? dropdown : new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            dropdown,
            new Text(helperText, style: c.Type.HelperText01.Merge(new TextStyle(Color: c.Colors.TextHelper))),
        ]);
    }
}

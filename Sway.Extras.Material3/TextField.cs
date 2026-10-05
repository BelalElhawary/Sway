using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed record InputDecoration(
    string? HintText = null,
    string? LabelText = null,
    string? HelperText = null,
    string? ErrorText = null,
    Widget? Prefix = null,
    Widget? Suffix = null,
    bool Filled = false,
    SKColor? FillColor = null,
    EdgeInsets? ContentPadding = null);

/// <summary>
/// The Material 3 text-field chrome: outlined or filled container, floating label, supporting text and prefix/suffix.
/// Used by <see cref="TextField"/> and <see cref="DropdownButton{T}"/>.
/// </summary>
public sealed class InputDecorator(InputDecoration decoration, Widget child, bool focused = false, bool hovered = false,
    bool hasContent = false, bool enabled = true, Key? key = null) : StatefulWidget(key)
{
    internal InputDecoration Decoration => decoration;
    internal Widget Child => child;
    internal bool Focused => focused;
    internal bool Hovered => hovered;
    internal bool HasContent => hasContent;
    internal bool Enabled => enabled;
    public override State CreateState() => new InputDecoratorState();
}

sealed class InputDecoratorState : TickerProviderState<InputDecorator>
{
    AnimationController _c = null!;
    CurvedAnimation _curve = null!;

    bool Floating => Widget.Focused || Widget.HasContent;

    public override void InitState()
    {
        _c = new AnimationController(this, TimeSpan.FromMilliseconds(150), value: Floating ? 1 : 0);
        _c.AddListener(() => { if (Mounted) SetState(); });
        _curve = new CurvedAnimation(_c, Curves.EaseOutCubic);
    }

    public override void DidUpdateWidget(InputDecorator old)
    {
        if (Floating) _c.Forward(); else _c.Reverse();
    }

    public override void Dispose()
    {
        _curve.Dispose();
        _c.Dispose();
        base.Dispose();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var d = Widget.Decoration;
        bool error = d.ErrorText is not null, enabled = Widget.Enabled;
        bool filled = d.Filled;
        bool hasLabel = d.LabelText is not null;
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        float t = _curve.Value;

        var borderColor = !enabled ? s.OnSurface.WithOpacity(0.12f) : error ? (Widget.Hovered && !Widget.Focused ? s.OnErrorContainer : s.Error)
            : Widget.Focused ? s.Primary : Widget.Hovered ? s.OnSurface : filled ? s.OnSurfaceVariant : s.Outline;
        float borderWidth = Widget.Focused ? 2 : 1;
        var labelColor = !enabled ? s.OnSurface.WithOpacity(0.38f) : error ? s.Error : Widget.Focused ? s.Primary : s.OnSurfaceVariant;

        var labelStyle = theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: labelColor));
        float labelWidth = hasLabel ? RenderParagraph.Measure(d.LabelText!, labelStyle, labelStyle.ToFont()) : 0;
        float startPad = d.Prefix is null ? 16 : 12;
        float labelStart = Lerps.Float(d.Prefix is null ? 16 : 52, 16, t);

        var padding = d.ContentPadding ?? (filled
            ? new EdgeInsets(startPad, hasLabel ? 24 : 16, d.Suffix is null ? 16 : 12, hasLabel ? 8 : 16)
            : new EdgeInsets(startPad, 16, d.Suffix is null ? 16 : 12, 16));
        if (rtl) padding = new EdgeInsets(padding.Right, padding.Top, padding.Left, padding.Bottom);

        Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            ..d.Prefix is null ? Array.Empty<Widget>() : [new Padding(EdgeInsetsDirectional.Only(end: 16), new IconTheme(s.OnSurfaceVariant, 24, d.Prefix))],
            new Expanded(Widget.Child),
            ..d.Suffix is null ? Array.Empty<Widget>() : [new Padding(EdgeInsetsDirectional.Only(start: 16), new IconTheme(s.OnSurfaceVariant, 24, d.Suffix))],
        ]);

        var fill = d.FillColor ?? s.SurfaceContainerHighest;
        var layers = new List<Widget>
        {
            Positioned.Fill(new CustomPaint(new FieldBorderPainter(filled, borderColor, borderWidth,
                filled ? (enabled ? fill : s.OnSurface.WithOpacity(0.04f)) : Colors.Transparent,
                labelStart - 4, hasLabel && !filled ? (labelWidth * 0.75f + 8) * t : 0, rtl))),
            new ConstrainedBox(new BoxConstraints(0, float.PositiveInfinity, 56, float.PositiveInfinity), new Padding(padding, content)),
        };

        if (hasLabel)
        {
            float top = filled ? Lerps.Float(16, 6, t) : Lerps.Float(16, -9, t);
            layers.Add(new PositionedDirectional(new IgnorePointer(Transform.Scale(Lerps.Float(1, 0.75f, t),
                new Text(d.LabelText!, style: labelStyle, softWrap: false), rtl ? Alignment.TopRight : Alignment.TopLeft)), start: labelStart, top: top));
        }

        Widget box = new Stack(layers, alignment: AlignmentDirectional.TopStart, clip: false);

        var support = d.ErrorText ?? d.HelperText;
        if (support is null) return box;
        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            box,
            new Padding(EdgeInsetsDirectional.Only(start: 16, end: 16, top: 4),
                new Text(support, style: theme.TextTheme.BodySmall.Merge(new TextStyle(Color: error ? s.Error : s.OnSurfaceVariant)))),
        ]);
    }
}

sealed class FieldBorderPainter(bool filled, SKColor color, float width, SKColor fill, float gapStart, float gapWidth, bool rtl) : CustomPainter
{
    public override void Paint(SKCanvas canvas, Size size)
    {
        if (filled)
        {
            var rr = new SKRoundRect();
            rr.SetRectRadii(new SKRect(0, 0, size.Width, size.Height), new[] { new SKPoint(4, 4), new SKPoint(4, 4), new SKPoint(0, 0), new SKPoint(0, 0) });
            using var fp = new SKPaint { Color = fill, IsAntialias = true };
            canvas.DrawRoundRect(rr, fp);
            using var lp = new SKPaint { Color = color, IsAntialias = false };
            canvas.DrawRect(0, size.Height - width, size.Width, width, lp);
            return;
        }

        canvas.Save();
        if (gapWidth > 0)
        {
            float gs = rtl ? size.Width - gapStart - gapWidth : gapStart;
            canvas.ClipRect(new SKRect(gs, -20, gs + gapWidth, width + 2), SKClipOperation.Difference);
        }
        using var p = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };
        canvas.DrawRoundRect(new SKRect(width / 2, width / 2, size.Width - width / 2, size.Height - width / 2), 4, 4, p);
        canvas.Restore();
    }

    public override bool ShouldRepaint(CustomPainter old) => true;
}

/// <summary>A Material 3 text input with floating label, hover/focus states and supporting text.</summary>
public sealed class TextField(TextEditingController? controller = null, FocusNode? focusNode = null, InputDecoration? decoration = null,
    TextStyle? style = null, bool obscureText = false, int? maxLines = 1, int? minLines = null, bool readOnly = false, bool enabled = true,
    Action<string>? onChanged = null, Action<string>? onSubmitted = null, bool autofocus = false, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController? Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal InputDecoration? Decoration => decoration;
    internal TextStyle? Style => style;
    internal bool Obscure => obscureText;
    internal int? MaxLines => maxLines;
    internal int? MinLines => minLines;
    internal bool ReadOnly => readOnly;
    internal bool Enabled => enabled;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal bool Autofocus => autofocus;

    public override State CreateState() => new TextFieldState();
}

sealed class TextFieldState : State<TextField>
{
    TextEditingController? _ownedController;
    FocusNode? _ownedNode;
    bool _hover;

    TextEditingController Controller => Widget.Controller ?? (_ownedController ??= new TextEditingController());
    FocusNode Node => Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "TextField" });

    public override void InitState()
    {
        Node.Changed += OnChange;
        Controller.Changed += OnChange;
        if (Widget.Autofocus)
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => { if (Mounted) Node.RequestFocus(); });
    }

    public override void DidUpdateWidget(TextField old)
    {
        if (!ReferenceEquals(old.Controller, Widget.Controller))
        {
            (old.Controller ?? _ownedController)!.Changed -= OnChange;
            Controller.Changed += OnChange;
        }
    }

    void OnChange() { if (Mounted) SetState(); }

    public override void Dispose()
    {
        Node.Changed -= OnChange;
        Controller.Changed -= OnChange;
    }

    public override Widget Build(BuildContext context)
    {
        var deco = Widget.Decoration ?? new InputDecoration();
        bool focused = Node.HasFocus;
        bool hasText = Controller.Text.Length > 0;
        bool showHint = deco.LabelText is null || focused;

        var theme = Theme.Of(Context);
        var scheme = theme.ColorScheme;
        var style = theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: scheme.OnSurface)).Merge(Widget.Style);
        var hintStyle = style.Merge(new TextStyle(Color: scheme.OnSurfaceVariant));

        Widget field = new EditableText(Controller, Node, style, hintStyle, showHint ? deco.HintText : null, Widget.Obscure, Widget.MaxLines, Widget.MinLines,
            Widget.ReadOnly || !Widget.Enabled, scheme.Primary, scheme.Primary.WithOpacity(0.35f), Widget.OnChanged, Widget.OnSubmitted);

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new InputDecorator(deco, field, focused, _hover, hasText, Widget.Enabled));
    }
}

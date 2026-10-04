using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Holds the text and selection of an editable field; read and write it from your own code.</summary>
public sealed class TextEditingController
{
    TextEditState _state;

    public TextEditingController(string text = "") { _state = new TextEditState(text, multiline: false); }

    internal TextEditState State => _state;

    /// <summary>Raised whenever the text or selection changes.</summary>
    public event Action? Changed;

    public string Text
    {
        get => _state.Value;
        set { if (_state.SetValueExternal(value)) Changed?.Invoke(); }
    }

    public int SelectionStart => _state.SelectionStart;
    public int SelectionEnd => _state.SelectionEnd;

    public void SelectAll() { _state.SelectAll(); Changed?.Invoke(); }
    public void Clear() => Text = "";

    internal void NotifyChanged() => Changed?.Invoke();

    /// <summary>Single-line and multi-line fields use different editing rules; swap the model if the field's mode changed.</summary>
    internal void EnsureMultiline(bool multiline)
    {
        if (_state.Multiline == multiline) return;
        _state = new TextEditState(_state.Value, multiline);
    }
}

/// <summary>The core editable text area: caret, selection, keyboard and clipboard, without decoration.</summary>
public sealed class EditableText(TextEditingController controller, FocusNode? focusNode = null, TextStyle? style = null,
    TextStyle? hintStyle = null, string? hintText = null, bool obscureText = false, int? maxLines = 1, int? minLines = null,
    bool readOnly = false, SKColor? cursorColor = null, SKColor? selectionColor = null, Action<string>? onChanged = null,
    Action<string>? onSubmitted = null, bool autofocus = false, Key? key = null) : StatefulWidget(key)
{
    internal TextEditingController Controller => controller;
    internal FocusNode? FocusNode => focusNode;
    internal TextStyle? Style => style;
    internal TextStyle? HintStyle => hintStyle;
    internal string? HintText => hintText;
    internal bool Obscure => obscureText;
    internal int? MaxLines => maxLines;
    internal int? MinLines => minLines;
    internal bool ReadOnly => readOnly;
    internal SKColor? CursorColor => cursorColor;
    internal SKColor? SelectionColor => selectionColor;
    internal Action<string>? OnChanged => onChanged;
    internal Action<string>? OnSubmitted => onSubmitted;
    internal bool Autofocus => autofocus;

    public override State CreateState() => new EditableTextState();
}

sealed class EditableTextState : State<EditableText>
{
    static readonly TimeSpan BlinkPeriod = TimeSpan.FromMilliseconds(530);

    FocusNode? _ownedNode;
    FocusNode _node = null!;
    RenderEditable? _render;
    bool _caretOn = true;
    bool _hadFocus;
    Action? _blinkAction;

    TextEditState Edit => Widget.Controller.State;
    bool Multiline => Widget.MaxLines != 1;

    public override void InitState()
    {
        Widget.Controller.EnsureMultiline(Multiline);
        AttachNode();
        Widget.Controller.Changed += OnControllerChanged;
    }

    void AttachNode()
    {
        _node = Widget.FocusNode ?? (_ownedNode ??= new FocusNode { DebugLabel = "EditableText" });
        _node.OnKey = HandleKey;
        _node.OnTextInput = HandleText;
        _node.Changed += OnFocusChanged;
        _hadFocus = _node.HasFocus;
    }

    void DetachNode()
    {
        _node.Changed -= OnFocusChanged;
        if (_node.OnKey == (Func<KeyEvent, bool>)HandleKey) _node.OnKey = null;
        _node.OnTextInput = null;
    }

    public override void DidUpdateWidget(EditableText old)
    {
        if (!ReferenceEquals(old.Controller, Widget.Controller))
        {
            old.Controller.Changed -= OnControllerChanged;
            Widget.Controller.Changed += OnControllerChanged;
        }
        Widget.Controller.EnsureMultiline(Multiline);
        if (!ReferenceEquals(old.FocusNode, Widget.FocusNode))
        {
            DetachNode();
            AttachNode();
        }
    }

    public override void Dispose()
    {
        Widget.Controller.Changed -= OnControllerChanged;
        StopBlink();
        DetachNode();
    }

    void OnControllerChanged() { if (Mounted) SetState(); }

    void OnFocusChanged()
    {
        bool has = _node.HasFocus;
        if (has == _hadFocus) return;
        _hadFocus = has;
        _caretOn = true;
        if (has) StartBlink(); else StopBlink();
        if (Mounted) SetState();
    }

    void StartBlink()
    {
        StopBlink();
        _blinkAction = () =>
        {
            if (!Mounted || !_node.HasFocus) return;
            _caretOn = !_caretOn;
            SetState();
            WidgetsBinding.Instance.ScheduleTimer(BlinkPeriod, _blinkAction!);
        };
        WidgetsBinding.Instance.ScheduleTimer(BlinkPeriod, _blinkAction);
    }

    void StopBlink()
    {
        if (_blinkAction is not null) WidgetsBinding.Instance.CancelTimer(_blinkAction);
        _blinkAction = null;
    }

    /// <summary>Typing or moving the caret keeps it solid until the next blink.</summary>
    void ResetBlink()
    {
        _caretOn = true;
        if (_node.HasFocus) StartBlink();
        if (Mounted) SetState();
    }

    void Changed(bool textChanged)
    {
        Widget.Controller.NotifyChanged();
        if (textChanged) Widget.OnChanged?.Invoke(Edit.Value);
        ResetBlink();
    }

    void HandleText(string text)
    {
        if (Widget.ReadOnly) return;
        Changed(Edit.Insert(text));
    }

    bool HandleKey(KeyEvent e)
    {
        if (!e.IsDown) return false;
        var edit = Edit;
        bool ro = Widget.ReadOnly;
        bool cmd = e.Command;

        if (cmd && !e.Alt)
        {
            switch (e.Key.ToLowerInvariant())
            {
                case "a": edit.SelectAll(); Changed(false); return true;
                case "c":
                    if (edit.HasSelection && !Widget.Obscure) WidgetsBinding.Instance.SetClipboard(edit.SelectedText);
                    return true;
                case "x":
                    if (edit.HasSelection && !Widget.Obscure)
                    {
                        WidgetsBinding.Instance.SetClipboard(edit.SelectedText);
                        if (!ro) Changed(edit.Insert("", "cut"));
                    }
                    return true;
                case "v":
                    if (!ro && WidgetsBinding.Instance.GetClipboard() is { Length: > 0 } clip) Changed(edit.Insert(clip, "paste"));
                    return true;
                case "z": if (!ro) Changed(e.Shift ? edit.Redo() : edit.Undo()); return true;
                case "y": if (!ro) Changed(edit.Redo()); return true;
            }
        }

        switch (e.Key)
        {
            case "Backspace" when !ro: Changed(edit.DeleteBackward(cmd)); return true;
            case "Delete" when !ro: Changed(edit.DeleteForward(cmd)); return true;
            case "ArrowLeft": edit.MoveHorizontal(-1, cmd, e.Shift); Moved(); return true;
            case "ArrowRight": edit.MoveHorizontal(1, cmd, e.Shift); Moved(); return true;
            case "ArrowUp" when Multiline: _render?.MoveVertical(-1, e.Shift); Changed(false); return true;
            case "ArrowDown" when Multiline: _render?.MoveVertical(1, e.Shift); Changed(false); return true;
            case "Home":
                if (cmd || !Multiline) edit.MoveTo(0, e.Shift); else _render?.MoveToLineEdge(false, e.Shift);
                Moved(); return true;
            case "End":
                if (cmd || !Multiline) edit.MoveTo(edit.Value.Length, e.Shift); else _render?.MoveToLineEdge(true, e.Shift);
                Moved(); return true;
            case "PageUp" when Multiline:
                for (int i = 0; i < (_render?.VisibleLineCount ?? 1); i++) _render?.MoveVertical(-1, e.Shift);
                Changed(false); return true;
            case "PageDown" when Multiline:
                for (int i = 0; i < (_render?.VisibleLineCount ?? 1); i++) _render?.MoveVertical(1, e.Shift);
                Changed(false); return true;
            case "Enter":
                if (Multiline && !ro) { Changed(edit.Insert("\n")); return true; }
                Widget.OnSubmitted?.Invoke(edit.Value);
                return true;
        }
        return false;
    }

    void Moved() => Changed(false);

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var scheme = theme.ColorScheme;
        var style = theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: scheme.OnSurface)).Merge(Widget.Style);
        var hint = style.Merge(new TextStyle(Color: scheme.OnSurfaceVariant)).Merge(Widget.HintStyle);
        bool focused = _node.HasFocus;
        var direction = Directionality.Of(context);

        return new Focus(focusNode: _node, child: new MouseRegion(cursor: MouseCursor.Text, child: new _Editable(
            Edit, style, hint, Widget.HintText, Widget.Obscure, Widget.MaxLines, Widget.MinLines, direction,
            Widget.CursorColor ?? scheme.Primary, Widget.SelectionColor ?? scheme.Primary.WithOpacity(0.35f), focused, _caretOn,
            r =>
            {
                _render = r;
                r.RequestFocus = () => _node.RequestFocus();
                r.OnSelectionChanged = () => { Widget.Controller.NotifyChanged(); ResetBlink(); };
            })));
    }
}

sealed class _Editable(TextEditState state, TextStyle style, TextStyle hintStyle, string? hint, bool obscure, int? maxLines,
    int? minLines, TextDirection direction, SKColor cursor, SKColor selection, bool focused, bool caretVisible,
    Action<RenderEditable> attach) : LeafRenderObjectWidget
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderEditable(state);
        attach(r);
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro)
    {
        var r = (RenderEditable)ro;
        attach(r);
        Apply(r);
    }

    void Apply(RenderEditable r) =>
        r.Update(state, style, hintStyle, hint, obscure, maxLines, minLines, direction, cursor, selection, focused, caretVisible);
}

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

        Widget field = new EditableText(Controller, Node, Widget.Style, null, showHint ? deco.HintText : null, Widget.Obscure, Widget.MaxLines, Widget.MinLines,
            Widget.ReadOnly || !Widget.Enabled, null, null, Widget.OnChanged, Widget.OnSubmitted);

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new InputDecorator(deco, field, focused, _hover, hasText, Widget.Enabled));
    }
}

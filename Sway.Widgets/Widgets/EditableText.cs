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

/// <summary>The editable text area: caret, selection, keyboard and clipboard, without decoration.</summary>
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
            case "ArrowLeft" when _render is not null: _render.MoveVisualHorizontal(-1, cmd, e.Shift); Moved(); return true;
            case "ArrowRight" when _render is not null: _render.MoveVisualHorizontal(1, cmd, e.Shift); Moved(); return true;
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
        // Text takes its look from the enclosing DefaultTextStyle; a design system's text field supplies its own style.
        var style = TextStyle.Fallback.Merge(DefaultTextStyle.Of(context)?.Style).Merge(Widget.Style);
        var hint = style.Merge(new TextStyle(Color: (style.Color ?? Colors.Black).WithOpacity(0.6f))).Merge(Widget.HintStyle);
        var cursorColor = Widget.CursorColor ?? style.Color ?? Colors.Black;
        bool focused = _node.HasFocus;
        var direction = Directionality.Of(context);

        return new Focus(focusNode: _node, child: new MouseRegion(cursor: MouseCursor.Text, child: new _Editable(
            Edit, style, hint, Widget.HintText, Widget.Obscure, Widget.MaxLines, Widget.MinLines, direction,
            cursorColor, Widget.SelectionColor ?? cursorColor.WithOpacity(0.35f), focused, _caretOn,
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

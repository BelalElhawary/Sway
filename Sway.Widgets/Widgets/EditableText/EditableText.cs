using SkiaSharp;

namespace Sway.Widgets;

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

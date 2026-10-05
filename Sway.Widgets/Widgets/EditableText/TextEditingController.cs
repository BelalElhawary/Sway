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

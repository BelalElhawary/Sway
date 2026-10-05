namespace Sway.Widgets;

/// <summary>A keyboard event with DOM-style key names ("Enter", "ArrowLeft", "a").</summary>
public sealed record KeyEvent(string Key, string Code, bool IsDown, bool Ctrl, bool Shift, bool Alt, bool Meta, bool Repeat = false)
{
    /// <summary>Ctrl on Windows/Linux (Cmd is not mapped separately).</summary>
    public bool Command => Ctrl || Meta;
}

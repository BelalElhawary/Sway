using Android.Views;

namespace Sway.Widgets;

/// <summary>Translates Android key codes for non-printing keys into DOM-style <c>key</c> names; printable keys are delivered as text.</summary>
static class AndroidKeyMap
{
    public static string? Translate(Keycode keyCode) => keyCode switch
    {
        Keycode.Enter or Keycode.NumpadEnter => "Enter",
        Keycode.Escape => "Escape",
        Keycode.Tab => "Tab",
        Keycode.Del => "Backspace",
        Keycode.ForwardDel => "Delete",
        Keycode.Insert => "Insert",
        Keycode.DpadLeft => "ArrowLeft",
        Keycode.DpadRight => "ArrowRight",
        Keycode.DpadUp => "ArrowUp",
        Keycode.DpadDown => "ArrowDown",
        Keycode.MoveHome => "Home",
        Keycode.MoveEnd => "End",
        Keycode.PageUp => "PageUp",
        Keycode.PageDown => "PageDown",
        _ => null,
    };
}

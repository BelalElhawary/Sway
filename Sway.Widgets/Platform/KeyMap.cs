using Silk.NET.Input;
using SilkKey = Silk.NET.Input.Key;

namespace Sway.Widgets;

/// <summary>
/// Translates Silk.NET keys into DOM-style <c>key</c> and <c>code</c> strings.
/// Printable keys assume a US layout until proper text input events (IME, layouts) are wired up.
/// </summary>
static class KeyMap
{
    static readonly Dictionary<SilkKey, string> Named = new()
    {
        [SilkKey.Enter] = "Enter", [SilkKey.Escape] = "Escape", [SilkKey.Tab] = "Tab",
        [SilkKey.Backspace] = "Backspace", [SilkKey.Delete] = "Delete", [SilkKey.Insert] = "Insert",
        [SilkKey.Left] = "ArrowLeft", [SilkKey.Right] = "ArrowRight", [SilkKey.Up] = "ArrowUp", [SilkKey.Down] = "ArrowDown",
        [SilkKey.Home] = "Home", [SilkKey.End] = "End", [SilkKey.PageUp] = "PageUp", [SilkKey.PageDown] = "PageDown",
        [SilkKey.ShiftLeft] = "Shift", [SilkKey.ShiftRight] = "Shift", [SilkKey.ControlLeft] = "Control", [SilkKey.ControlRight] = "Control",
        [SilkKey.AltLeft] = "Alt", [SilkKey.AltRight] = "Alt", [SilkKey.SuperLeft] = "Meta", [SilkKey.SuperRight] = "Meta",
        [SilkKey.CapsLock] = "CapsLock", [SilkKey.Space] = " ",
    };

    // Unshifted and shifted characters for punctuation keys on a US layout.
    static readonly Dictionary<SilkKey, (string plain, string shifted, string code)> Punctuation = new()
    {
        [SilkKey.Comma] = (",", "<", "Comma"), [SilkKey.Period] = (".", ">", "Period"), [SilkKey.Slash] = ("/", "?", "Slash"),
        [SilkKey.Semicolon] = (";", ":", "Semicolon"), [SilkKey.Apostrophe] = ("'", "\"", "Quote"),
        [SilkKey.LeftBracket] = ("[", "{", "BracketLeft"), [SilkKey.RightBracket] = ("]", "}", "BracketRight"),
        [SilkKey.BackSlash] = ("\\", "|", "Backslash"), [SilkKey.Minus] = ("-", "_", "Minus"), [SilkKey.Equal] = ("=", "+", "Equal"),
        [SilkKey.GraveAccent] = ("`", "~", "Backquote"),
    };

    const string ShiftedDigits = ")!@#$%^&*(";

    // DOM KeyboardEvent.location values.
    const float Standard = 0, Left = 1, Right = 2, Numpad = 3;

    static readonly HashSet<SilkKey> LeftKeys = new() { SilkKey.ShiftLeft, SilkKey.ControlLeft, SilkKey.AltLeft, SilkKey.SuperLeft };
    static readonly HashSet<SilkKey> RightKeys = new() { SilkKey.ShiftRight, SilkKey.ControlRight, SilkKey.AltRight, SilkKey.SuperRight };

    public static (string key, string code, float location) Translate(SilkKey key, bool shift)
    {
        if (key == SilkKey.KeypadEnter) return ("Enter", "NumpadEnter", Numpad);

        if (Named.TryGetValue(key, out var named))
        {
            float location = LeftKeys.Contains(key) ? Left : RightKeys.Contains(key) ? Right : Standard;
            return (named, key == SilkKey.Space ? "Space" : key.ToString(), location);
        }

        if (key >= SilkKey.A && key <= SilkKey.Z)
        {
            string letter = ((char)('a' + (key - SilkKey.A))).ToString();
            return (shift ? letter.ToUpperInvariant() : letter, "SilkKey" + letter.ToUpperInvariant(), Standard);
        }

        if (key >= SilkKey.Number0 && key <= SilkKey.Number9)
        {
            int digit = key - SilkKey.Number0;
            return (shift ? ShiftedDigits[digit].ToString() : digit.ToString(), "Digit" + digit, Standard);
        }

        if (key >= SilkKey.Keypad0 && key <= SilkKey.Keypad9)
            return ((key - SilkKey.Keypad0).ToString(), "Numpad" + (key - SilkKey.Keypad0), Numpad);

        if (key >= SilkKey.F1 && key <= SilkKey.F12)
            return (key.ToString(), key.ToString(), Standard);

        if (Punctuation.TryGetValue(key, out var p))
            return (shift ? p.shifted : p.plain, p.code, Standard);

        return (key.ToString(), key.ToString(), Standard);
    }
}

using Silk.NET.Input;

namespace Sway.Core.Platform;

/// <summary>
/// Translates Silk.NET keys into DOM-style <c>key</c> and <c>code</c> strings.
/// Printable keys assume a US layout until proper text input events (IME, layouts) are wired up.
/// </summary>
static class KeyMap
{
    static readonly Dictionary<Key, string> Named = new()
    {
        [Key.Enter] = "Enter", [Key.KeypadEnter] = "Enter", [Key.Escape] = "Escape", [Key.Tab] = "Tab",
        [Key.Backspace] = "Backspace", [Key.Delete] = "Delete", [Key.Insert] = "Insert",
        [Key.Left] = "ArrowLeft", [Key.Right] = "ArrowRight", [Key.Up] = "ArrowUp", [Key.Down] = "ArrowDown",
        [Key.Home] = "Home", [Key.End] = "End", [Key.PageUp] = "PageUp", [Key.PageDown] = "PageDown",
        [Key.ShiftLeft] = "Shift", [Key.ShiftRight] = "Shift", [Key.ControlLeft] = "Control", [Key.ControlRight] = "Control",
        [Key.AltLeft] = "Alt", [Key.AltRight] = "Alt", [Key.SuperLeft] = "Meta", [Key.SuperRight] = "Meta",
        [Key.CapsLock] = "CapsLock", [Key.Space] = " ",
    };

    // Unshifted and shifted characters for punctuation keys on a US layout.
    static readonly Dictionary<Key, (string plain, string shifted, string code)> Punctuation = new()
    {
        [Key.Comma] = (",", "<", "Comma"), [Key.Period] = (".", ">", "Period"), [Key.Slash] = ("/", "?", "Slash"),
        [Key.Semicolon] = (";", ":", "Semicolon"), [Key.Apostrophe] = ("'", "\"", "Quote"),
        [Key.LeftBracket] = ("[", "{", "BracketLeft"), [Key.RightBracket] = ("]", "}", "BracketRight"),
        [Key.BackSlash] = ("\\", "|", "Backslash"), [Key.Minus] = ("-", "_", "Minus"), [Key.Equal] = ("=", "+", "Equal"),
        [Key.GraveAccent] = ("`", "~", "Backquote"),
    };

    const string ShiftedDigits = ")!@#$%^&*(";

    public static (string key, string code) Translate(Key key, bool shift)
    {
        if (Named.TryGetValue(key, out var named))
            return (named, key == Key.Space ? "Space" : key.ToString());

        if (key >= Key.A && key <= Key.Z)
        {
            string letter = ((char)('a' + (key - Key.A))).ToString();
            return (shift ? letter.ToUpperInvariant() : letter, "Key" + letter.ToUpperInvariant());
        }

        if (key >= Key.Number0 && key <= Key.Number9)
        {
            int digit = key - Key.Number0;
            return (shift ? ShiftedDigits[digit].ToString() : digit.ToString(), "Digit" + digit);
        }

        if (key >= Key.Keypad0 && key <= Key.Keypad9)
            return ((key - Key.Keypad0).ToString(), "Numpad" + (key - Key.Keypad0));

        if (key >= Key.F1 && key <= Key.F12)
            return (key.ToString(), key.ToString());

        if (Punctuation.TryGetValue(key, out var p))
            return (shift ? p.shifted : p.plain, p.code);

        return (key.ToString(), key.ToString());
    }

    /// <summary>Maps a CSS cursor keyword to the closest cursor the windowing layer offers.</summary>
    public static StandardCursor Cursor(string css) => css switch
    {
        "pointer" => StandardCursor.Hand,
        "text" or "vertical-text" => StandardCursor.IBeam,
        "crosshair" => StandardCursor.Crosshair,
        "ew-resize" or "col-resize" or "e-resize" or "w-resize" => StandardCursor.HResize,
        "ns-resize" or "row-resize" or "n-resize" or "s-resize" => StandardCursor.VResize,
        _ => StandardCursor.Default
    };
}

using System.Globalization;

namespace Sway.Widgets;

/// <summary>
/// Editing model for input and textarea: text, caret, selection and undo. It knows nothing about
/// fonts or pixels; layout fills <see cref="Lines"/> and the controller moves the caret by index.
/// Indices are UTF-16 offsets that always sit on grapheme boundaries.
/// </summary>
public sealed class TextEditState
{
    sealed record Snapshot(string Value, int Caret, int Anchor);

    const int UndoLimit = 100;

    readonly List<Snapshot> _undo = new();
    readonly List<Snapshot> _redo = new();
    string _lastEditKind = "";

    public TextEditState(string value, bool multiline)
    {
        Value = value;
        Multiline = multiline;
        Caret = Anchor = value.Length;
    }

    public string Value { get; private set; }
    public bool Multiline { get; }
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public int MaxLength { get; set; } = -1;

    /// <summary>Inner scroll of the text inside the box, in pixels.</summary>
    public float ScrollX { get; set; }
    public float ScrollY { get; set; }

    /// <summary>Bumped on every change to the text, so layout can tell when its cached lines are stale.</summary>
    public int Version { get; private set; }

    // Filled by TextControls for the current width; always at least one line.
    public List<TextLine> Lines { get; } = new() { new TextLine(0, 0, true) };
    public int LinesVersion { get; set; } = -1;
    public float LinesWidth { get; set; } = -1;

    public bool HasSelection => Caret != Anchor;
    public int SelectionStart => Math.Min(Caret, Anchor);
    public int SelectionEnd => Math.Max(Caret, Anchor);
    public string SelectedText => Value.Substring(SelectionStart, SelectionEnd - SelectionStart);

    // ---- value changes ----

    /// <summary>Adopts a value that came from the model (the value attribute) rather than the user.</summary>
    public bool SetValueExternal(string value)
    {
        if (value == Value) return false;
        Value = value;
        Caret = Anchor = value.Length;
        Version++;
        return true;
    }

    /// <summary>Replaces the selection (or inserts at the caret). Returns true if the text changed.</summary>
    public bool Insert(string text, string kind = "type")
    {
        if (!Multiline) text = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        else text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        if (MaxLength >= 0)
        {
            int room = MaxLength - (Value.Length - (SelectionEnd - SelectionStart));
            if (room <= 0) return false;
            if (text.Length > room) text = TrimToBoundary(text, room);
        }
        if (text.Length == 0 && !HasSelection) return false;

        Remember(kind);
        Value = string.Concat(Value.AsSpan(0, SelectionStart), text, Value.AsSpan(SelectionEnd));
        Caret = Anchor = SelectionStart + text.Length;
        Version++;
        return true;
    }

    static string TrimToBoundary(string text, int max)
    {
        if (max <= 0) return "";
        // Do not split a surrogate pair.
        if (char.IsHighSurrogate(text[max - 1])) max--;
        return text[..max];
    }

    public bool DeleteBackward(bool byWord)
    {
        if (HasSelection) return Insert("", "delete");
        if (Caret == 0) return false;

        int target = byWord ? WordLeft(Caret) : PreviousBoundary(Caret);
        Remember("delete-back");
        Value = Value.Remove(target, Caret - target);
        Caret = Anchor = target;
        Version++;
        return true;
    }

    public bool DeleteForward(bool byWord)
    {
        if (HasSelection) return Insert("", "delete");
        if (Caret >= Value.Length) return false;

        int target = byWord ? WordRight(Caret) : NextBoundary(Caret);
        Remember("delete-forward");
        Value = Value.Remove(Caret, target - Caret);
        Anchor = Caret;
        Version++;
        return true;
    }

    // ---- caret and selection ----

    public void MoveTo(int index, bool extend)
    {
        Caret = Math.Clamp(index, 0, Value.Length);
        if (!extend) Anchor = Caret;
        _lastEditKind = "";
    }

    /// <summary>Left/right collapse a selection to its edge unless extending, like native fields.</summary>
    public void MoveHorizontal(int direction, bool byWord, bool extend)
    {
        if (!extend && HasSelection)
        {
            MoveTo(direction < 0 ? SelectionStart : SelectionEnd, false);
            return;
        }

        int target = direction < 0
            ? (byWord ? WordLeft(Caret) : PreviousBoundary(Caret))
            : (byWord ? WordRight(Caret) : NextBoundary(Caret));
        MoveTo(target, extend);
    }

    public void SelectAll()
    {
        Anchor = 0;
        Caret = Value.Length;
        _lastEditKind = "";
    }

    public void SelectWordAt(int index)
    {
        index = Math.Clamp(index, 0, Value.Length);
        int start = index, end = index;
        while (start > 0 && IsWordChar(Value[start - 1])) start--;
        while (end < Value.Length && IsWordChar(Value[end])) end++;

        // Clicking on punctuation or whitespace selects that single run instead.
        if (start == end)
        {
            if (end < Value.Length) end = NextBoundary(end);
            else if (start > 0) start = PreviousBoundary(start);
        }
        Anchor = start;
        Caret = end;
    }

    public void SelectLineAt(int index)
    {
        index = Math.Clamp(index, 0, Value.Length);
        int start = index == 0 ? 0 : Value.LastIndexOf('\n', index - 1) + 1;
        int end = Value.IndexOf('\n', index);
        Anchor = start;
        Caret = end < 0 ? Value.Length : end;
    }

    static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    public int WordLeft(int index)
    {
        while (index > 0 && char.IsWhiteSpace(Value[index - 1])) index--;
        if (index > 0 && IsWordChar(Value[index - 1]))
            while (index > 0 && IsWordChar(Value[index - 1])) index--;
        else if (index > 0)
            index = PreviousBoundary(index);
        return index;
    }

    public int WordRight(int index)
    {
        if (index < Value.Length && IsWordChar(Value[index]))
            while (index < Value.Length && IsWordChar(Value[index])) index++;
        else if (index < Value.Length)
            index = NextBoundary(index);
        while (index < Value.Length && char.IsWhiteSpace(Value[index]) && Value[index] != '\n') index++;
        return index;
    }

    // ---- grapheme boundaries ----

    public int PreviousBoundary(int index)
    {
        if (index <= 0) return 0;
        int previous = 0;
        foreach (int start in StringInfo.ParseCombiningCharacters(Value))
        {
            if (start >= index) break;
            previous = start;
        }
        return previous;
    }

    public int NextBoundary(int index)
    {
        if (index >= Value.Length) return Value.Length;
        var starts = StringInfo.ParseCombiningCharacters(Value);
        foreach (int start in starts)
            if (start > index) return start;
        return Value.Length;
    }

    // ---- line navigation (uses lines supplied by layout) ----

    /// <summary>Index of the visual line holding the caret. At a soft wrap the caret belongs to the line that follows.</summary>
    public int LineOfIndex(int index)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            if (index < line.End) return i;
            if (index == line.End && (line.HardBreak || i == Lines.Count - 1)) return i;
        }
        return Lines.Count - 1;
    }

    // ---- undo ----

    // Consecutive typing (or consecutive backspaces) collapses into a single undo step.
    void Remember(string kind)
    {
        // Only runs of typing or repeated deletes merge; pastes, cuts and the like are separate steps.
        bool mergeable = kind is "type" or "delete-back" or "delete-forward";
        if (!mergeable || kind != _lastEditKind || _undo.Count == 0)
        {
            _undo.Add(new Snapshot(Value, Caret, Anchor));
            if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        }
        _lastEditKind = kind;
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Add(new Snapshot(Value, Caret, Anchor));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Add(new Snapshot(Value, Caret, Anchor));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }

    void Restore(Snapshot s)
    {
        Value = s.Value;
        Caret = s.Caret;
        Anchor = s.Anchor;
        Version++;
        _lastEditKind = "";
    }
}

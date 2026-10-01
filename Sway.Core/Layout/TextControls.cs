using System.Globalization;
using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>
/// Geometry for text controls: wrapping a textarea into visual lines, caret and selection positions,
/// mapping a pointer position to a text index, and keeping the caret scrolled into view.
/// All x/y values here are in layout space unless a name says otherwise.
/// </summary>
public static class TextControls
{
    public const char PasswordBullet = '•';
    public const float DefaultCheckSize = 13;
    public const float SelectArrowWidth = 22;

    public static TextEditState GetEdit(ElementNode el)
    {
        if (el.Edit is { } existing) return existing;

        string initial = el.GetAttribute("value") ?? (el.Tag == "textarea" ? Controls.TextContent(el) : "");
        var edit = new TextEditState(initial, multiline: el.Tag == "textarea");
        if (el.GetAttribute("maxlength") is { } max && int.TryParse(max, out var n)) edit.MaxLength = n;
        el.Edit = edit;
        return edit;
    }

    /// <summary>The text as drawn: password fields show bullets, one per UTF-16 unit so indices still line up.</summary>
    public static string DisplayText(ElementNode el, TextEditState edit) =>
        Controls.IsPassword(el) ? new string(PasswordBullet, edit.Value.Length) : edit.Value;

    public static float LineHeight(ElementNode el) => FontCache.LineHeight(el.Style);

    static SKFont Font(ElementNode el) => FontCache.Get(el.Style);

    public static float Measure(ElementNode el, string text) => text.Length == 0 ? 0 : Font(el).MeasureText(text);

    /// <summary>Offset from a line box top to the text baseline, centring glyphs in the line height.</summary>
    public static float BaselineOffset(ElementNode el)
    {
        var m = Font(el).Metrics;
        float content = m.Descent - m.Ascent;
        return (LineHeight(el) - content) / 2 - m.Ascent;
    }

    // ---- intrinsic size ----

    static float AverageCharWidth(ElementNode el) => Math.Max(1, Measure(el, "0"));

    /// <summary>Content-box width a control asks for when its width is auto.</summary>
    public static float IntrinsicContentWidth(ElementNode el)
    {
        if (Controls.IsCheckable(el)) return DefaultCheckSize;
        if (Controls.IsTextArea(el)) return Controls.IntAttribute(el, "cols", 20) * AverageCharWidth(el);
        if (Controls.IsButtonInput(el)) return Measure(el, ButtonLabel(el));

        if (Controls.IsSelect(el))
        {
            var options = Controls.Options(el);
            float widest = options.Count == 0 ? AverageCharWidth(el) * 4 : options.Max(o => Measure(el, o.Label));
            return widest;
        }

        return Controls.IntAttribute(el, "size", 20) * AverageCharWidth(el);
    }

    public static float IntrinsicContentHeight(ElementNode el)
    {
        if (Controls.IsCheckable(el)) return DefaultCheckSize;
        if (Controls.IsTextArea(el)) return Controls.IntAttribute(el, "rows", 2) * LineHeight(el);
        return LineHeight(el);
    }

    public static string ButtonLabel(ElementNode el) =>
        el.GetAttribute("value") ?? Controls.InputType(el) switch { "submit" => "Submit", "reset" => "Reset", _ => "" };

    // ---- line layout ----

    /// <summary>Recomputes visual lines when the text or the available width changed.</summary>
    public static void EnsureLines(ElementNode el, TextEditState edit)
    {
        float width = Math.Max(1, el.ContentRect.Width);
        if (edit.LinesVersion == edit.Version && Math.Abs(edit.LinesWidth - width) < 0.01f) return;

        edit.LinesVersion = edit.Version;
        edit.LinesWidth = width;
        edit.Lines.Clear();

        string text = DisplayText(el, edit);
        if (!edit.Multiline)
        {
            edit.Lines.Add(new TextLine(0, text.Length, true));
            return;
        }

        int start = 0;
        while (true)
        {
            int newline = text.IndexOf('\n', start);
            int hardEnd = newline < 0 ? text.Length : newline;
            WrapSegment(el, text, start, hardEnd, width, edit.Lines);
            if (newline < 0) break;
            start = newline + 1;
        }
    }

    static void WrapSegment(ElementNode el, string text, int start, int end, float width, List<TextLine> lines)
    {
        if (start == end) { lines.Add(new TextLine(start, end, true)); return; }

        int s = start;
        while (s < end)
        {
            int fit = FitCount(el, text, s, end, width);
            if (s + fit >= end)
            {
                lines.Add(new TextLine(s, end, true));
                return;
            }

            // Prefer breaking after the last space; otherwise break inside the word.
            int space = text.LastIndexOf(' ', s + fit - 1, fit);
            int lineEnd = space > s ? space + 1 : s + Math.Max(1, fit);
            lines.Add(new TextLine(s, lineEnd, false));
            s = lineEnd;
        }
    }

    // Largest n such that text[s..s+n] fits in width (binary search; widths grow with n).
    static int FitCount(ElementNode el, string text, int s, int end, float width)
    {
        int lo = 0, hi = end - s;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Measure(el, text.Substring(s, mid)) <= width) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    // ---- positions ----

    /// <summary>X of an index within its line, relative to the start of the text (before scrolling).</summary>
    public static float XOfIndex(ElementNode el, TextEditState edit, int index)
    {
        EnsureLines(el, edit);
        var line = edit.Lines[edit.LineOfIndex(index)];
        string text = DisplayText(el, edit);
        int from = Math.Min(line.Start, text.Length);
        int to = Math.Clamp(index, from, text.Length);
        return Measure(el, text.Substring(from, to - from));
    }

    /// <summary>Top of the visual line (layout space) taking inner scroll into account.</summary>
    public static float LineTop(ElementNode el, TextEditState edit, int lineIndex)
    {
        var content = el.ContentRect;
        return edit.Multiline
            ? content.Top + lineIndex * LineHeight(el) - edit.ScrollY
            : content.Top + (content.Height - LineHeight(el)) / 2;
    }

    public static float TextOriginX(ElementNode el, TextEditState edit) => el.ContentRect.Left - edit.ScrollX;

    /// <summary>Maps a point in layout space to the nearest caret index.</summary>
    public static int IndexFromPoint(ElementNode el, TextEditState edit, float x, float y)
    {
        EnsureLines(el, edit);
        int lineIndex = 0;
        if (edit.Multiline)
        {
            float lh = LineHeight(el);
            lineIndex = Math.Clamp((int)Math.Floor((y - el.ContentRect.Top + edit.ScrollY) / lh), 0, edit.Lines.Count - 1);
        }
        return IndexInLine(el, edit, lineIndex, x - TextOriginX(el, edit));
    }

    /// <summary>Index in a line whose x (from the line start) is closest to <paramref name="localX"/>.</summary>
    static int IndexInLine(ElementNode el, TextEditState edit, int lineIndex, float localX)
    {
        var line = edit.Lines[lineIndex];
        string text = DisplayText(el, edit);

        // Candidate caret positions are grapheme boundaries inside the line, plus its end.
        var boundaries = new List<int>();
        foreach (int b in StringInfo.ParseCombiningCharacters(edit.Value))
            if (b >= line.Start && b < line.End) boundaries.Add(b);
        if (boundaries.Count == 0 || boundaries[0] != line.Start) boundaries.Insert(0, line.Start);
        boundaries.Add(line.End);

        float WidthAt(int index) => Measure(el, text.Substring(line.Start, index - line.Start));

        int lo = 0, hi = boundaries.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (WidthAt(boundaries[mid]) < localX) lo = mid + 1; else hi = mid;
        }
        if (lo > 0 && localX - WidthAt(boundaries[lo - 1]) < WidthAt(boundaries[lo]) - localX) lo--;

        int result = boundaries[lo];
        // The end of a soft-wrapped line is the same spot as the next line's start; stay on this line.
        if (result == line.End && !line.HardBreak && lo > 0) result = boundaries[lo - 1];
        return result;
    }

    /// <summary>Moves the caret up or down by visual lines, keeping its x position.</summary>
    public static int MoveVertical(ElementNode el, TextEditState edit, int caret, int lines)
    {
        EnsureLines(el, edit);
        int current = edit.LineOfIndex(caret);
        int target = Math.Clamp(current + lines, 0, edit.Lines.Count - 1);
        if (target == current) return lines < 0 ? 0 : edit.Value.Length;
        return IndexInLine(el, edit, target, XOfIndex(el, edit, caret));
    }

    public static int LineStartIndex(ElementNode el, TextEditState edit, int caret)
    {
        EnsureLines(el, edit);
        return edit.Lines[edit.LineOfIndex(caret)].Start;
    }

    public static int LineEndIndex(ElementNode el, TextEditState edit, int caret)
    {
        EnsureLines(el, edit);
        var line = edit.Lines[edit.LineOfIndex(caret)];
        // Stop before the trailing space of a soft wrap so the caret stays on this line.
        return !line.HardBreak && line.End > line.Start ? line.End - 1 : line.End;
    }

    // ---- scrolling ----

    /// <summary>Scrolls the text inside the box so the caret is visible.</summary>
    public static void EnsureCaretVisible(ElementNode el, TextEditState edit)
    {
        EnsureLines(el, edit);
        var content = el.ContentRect;

        if (!edit.Multiline)
        {
            float textWidth = Measure(el, DisplayText(el, edit));
            float caretX = XOfIndex(el, edit, edit.Caret);
            if (caretX - edit.ScrollX > content.Width - 1) edit.ScrollX = caretX - content.Width + 1;
            if (caretX - edit.ScrollX < 0) edit.ScrollX = caretX;
            edit.ScrollX = Math.Clamp(edit.ScrollX, 0, Math.Max(0, textWidth - content.Width + 1));
            return;
        }

        float lh = LineHeight(el);
        int line = edit.LineOfIndex(edit.Caret);
        float top = line * lh, bottom = top + lh;
        if (top < edit.ScrollY) edit.ScrollY = top;
        else if (bottom > edit.ScrollY + content.Height) edit.ScrollY = bottom - content.Height;
        edit.ScrollY = Math.Clamp(edit.ScrollY, 0, MaxScrollY(el, edit));
    }

    public static float MaxScrollY(ElementNode el, TextEditState edit)
    {
        EnsureLines(el, edit);
        return Math.Max(0, edit.Lines.Count * LineHeight(el) - el.ContentRect.Height);
    }
}

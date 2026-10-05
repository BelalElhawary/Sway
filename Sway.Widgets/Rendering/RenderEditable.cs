using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Lays out, paints and hit-tests a <see cref="TextEditState"/> (caret, selection, wrapping, inner scroll).</summary>
public sealed class RenderEditable : RenderBox
{
    // X holds the caret x of every index in the line, relative to the line's left origin; Width is its total advance.
    sealed record Run(int Start, int Length, float Origin, float[] Local);
    sealed record Line(int Start, int End, bool HardBreak, float Width, float[] X, List<Run> Runs);

    TextEditState _state;
    TextStyle _style;
    TextStyle _hintStyle;
    string? _hint;
    bool _obscure;
    int? _maxLines, _minLines;
    TextDirection _direction;
    SKColor _cursorColor, _selectionColor;
    bool _focused, _caretVisible = true;
    List<Line> _lines = new();
    float _lineHeight, _baseline;
    int _version = -1, _caret = -1;

    // multi-click tracking
    long _lastDownMs;
    Offset _lastDownPos;
    int _clickCount;
    bool _dragging;

    public Action? RequestFocus;
    public Action? OnSelectionChanged;

    public RenderEditable(TextEditState state) { _state = state; _style = TextStyle.Fallback; _hintStyle = _style; }

    public void Update(TextEditState state, TextStyle style, TextStyle hintStyle, string? hint, bool obscure, int? maxLines, int? minLines,
        TextDirection direction, SKColor cursorColor, SKColor selectionColor, bool focused, bool caretVisible)
    {
        bool layout = !ReferenceEquals(_state, state) || !_style.Equals(style) || _obscure != obscure || _maxLines != maxLines
            || _minLines != minLines || _direction != direction || _version != state.Version || _caret != state.Caret
            || (_hint is null) != (hint is null);
        _state = state; _style = style; _hintStyle = hintStyle; _hint = hint; _obscure = obscure; _maxLines = maxLines;
        _minLines = minLines; _direction = direction; _cursorColor = cursorColor; _selectionColor = selectionColor;
        if (layout) MarkNeedsLayout();
        if (_focused != focused || _caretVisible != caretVisible) { _focused = focused; _caretVisible = caretVisible; MarkNeedsPaint(); }
    }

    public override void VisitChildren(Action<RenderObject> visitor) { }

    bool Multiline => _maxLines != 1;
    string Display => _obscure ? new string('•', _state.Value.Length) : _state.Value;
    SKFont Font => _style.ToFont();

    // ---- layout ----

    protected override void PerformLayout()
    {
        var font = Font;
        (_lineHeight, _baseline) = RenderParagraph.LineMetrics(_style, font.Metrics);

        float width = Constraints.HasBoundedWidth ? Constraints.MaxWidth : Math.Max(Constraints.MinWidth, 200);
        BuildLines(width, font);

        int shown = Multiline
            ? Math.Clamp(_lines.Count, _minLines ?? 1, _maxLines ?? int.MaxValue)
            : 1;
        Size = Constraints.Constrain(new Size(width, shown * _lineHeight));
        _version = _state.Version;
        _caret = _state.Caret;
        EnsureCaretVisible();
    }

    void BuildLines(float width, SKFont font)
    {
        string text = Display;
        _lines = new List<Line>();
        _state.Lines.Clear();

        void Add(int start, int end, bool hard)
        {
            var x = LineOffsets(text.Substring(start, end - start), font, out float width, out var runs);
            _lines.Add(new Line(start, end, hard, width, x, runs));
            _state.Lines.Add(new TextLine(start, end, hard));
        }

        int pos = 0;
        while (true)
        {
            int nl = Multiline ? text.IndexOf('\n', pos) : -1;
            int hardEnd = nl < 0 ? text.Length : nl;
            if (!Multiline) { Add(0, text.Length, true); break; }

            // Soft-wrap this paragraph at word boundaries; very long words break by character.
            int start = pos;
            while (start < hardEnd)
            {
                int end = FitLine(text, start, hardEnd, width, font);
                bool lastOfPara = end >= hardEnd;
                Add(start, end, lastOfPara);
                start = end;
                if (lastOfPara) break;
            }
            if (hardEnd == pos) Add(pos, pos, true); // empty paragraph
            if (nl < 0) break;
            pos = nl + 1;
            if (pos == text.Length && nl >= 0) { Add(pos, pos, true); break; }
        }
        if (_lines.Count == 0) Add(0, 0, true);
    }

    int FitLine(string text, int start, int hardEnd, float width, SKFont font)
    {
        // Prefixes are always measured at a grapheme boundary: a prefix cut inside a cluster does not measure monotonically.
        int Snap(int index) => Math.Min(_state.NextBoundary(index - 1), hardEnd);
        bool Overflows(int end) => RenderParagraph.Measure(text.Substring(start, end - start), _style, font) > width;

        // The line breaks before the first grapheme whose prefix overflows, but always keeps at least one grapheme.
        // Prefix width only grows with length: gallop to a prefix that overflows (so a long paragraph is never
        // measured whole for every line), then binary-search the exact overflow point.
        int low = Math.Min(_state.NextBoundary(start), hardEnd) + 1, high = -1;
        for (int step = 32; high < 0; step *= 2)
        {
            int probe = low - 1 + step;
            if (probe >= hardEnd)
            {
                if (!Overflows(hardEnd)) return hardEnd;
                high = hardEnd;
            }
            else if (Overflows(Snap(probe))) high = probe;
            else low = probe + 1;
        }

        int breakAt = hardEnd;
        if (low <= hardEnd)
        {
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (Overflows(Snap(mid))) high = mid; else low = mid + 1;
            }
            breakAt = _state.PreviousBoundary(Snap(low));
        }

        // Prefer breaking after the last space that precedes the overflow.
        int space = breakAt > start ? text.LastIndexOf(' ', breakAt - 1, breakAt - start) : -1;
        if (space >= start) return space + 1;
        return Math.Max(breakAt, _state.NextBoundary(start));
    }

    /// <summary>Caret x of each index in a line, in visual order. Runs follow the field direction, so an empty RTL field still has its caret on the right.</summary>
    float[] LineOffsets(string s, SKFont font, out float width, out List<Run> placed)
    {
        var offsets = new float[s.Length + 1];
        width = 0;
        placed = new List<Run>();
        if (s.Length == 0) return offsets;

        var runs = Bidi.Analyze(s, _direction);
        IEnumerable<BidiRun> visual = _direction == TextDirection.Rtl ? Enumerable.Reverse(runs) : runs;
        float origin = 0;
        foreach (var run in visual)
        {
            string runText = s.Substring(run.Start, run.Length);
            var local = TextShaper.CaretOffsets(runText, font, run.Direction == TextDirection.Rtl);
            // Runs without RTL characters are painted one character at a time with letter spacing; mirror that advance.
            if (_style.LetterSpacing is { } ls && ls != 0 && !TextShaper.ContainsRtl(runText))
            {
                float acc = 0;
                for (int i = 1; i <= runText.Length; i++) { acc += font.MeasureText(runText[i - 1].ToString()) + ls; local[i] = acc; }
            }
            for (int i = 0; i <= run.Length; i++) offsets[run.Start + i] = origin + local[i];
            placed.Add(new Run(run.Start, run.Length, origin, local));
            origin += local.Max(); // an RTL run starts at its right edge, so its width is not the last offset
        }
        width = origin;
        return offsets;
    }

    // ---- geometry ----

    int LineIndexOf(int index) => _state.LineOfIndex(index);

    float LineX0(Line l) => _direction == TextDirection.Rtl ? Size.Width - l.Width : 0;

    float XOf(Line l, int index)
    {
        index = Math.Clamp(index, l.Start, l.End);
        return LineX0(l) + l.X[index - l.Start];
    }

    int IndexAt(Line l, float x)
    {
        int best = l.Start;
        float bestDist = float.MaxValue;
        for (int i = l.Start; ; i = _state.NextBoundary(i))
        {
            float d = Math.Abs(XOf(l, i) - x);
            if (d < bestDist) { bestDist = d; best = i; }
            if (i >= l.End) break;
            if (_state.NextBoundary(i) > l.End) { if (Math.Abs(XOf(l, l.End) - x) < bestDist) best = l.End; break; }
        }
        return best;
    }

    Offset CaretPosition(int index)
    {
        int li = Math.Clamp(LineIndexOf(index), 0, _lines.Count - 1);
        return new Offset(XOf(_lines[li], index), li * _lineHeight);
    }

    void EnsureCaretVisible()
    {
        if (_lines.Count == 0) return;
        var caret = CaretPosition(_state.Caret);
        if (Multiline)
        {
            float content = _lines.Count * _lineHeight;
            float max = Math.Max(0, content - Size.Height);
            float y = _state.ScrollY;
            if (caret.Dy < y) y = caret.Dy;
            else if (caret.Dy + _lineHeight > y + Size.Height) y = caret.Dy + _lineHeight - Size.Height;
            _state.ScrollY = Math.Clamp(y, 0, max);
            _state.ScrollX = 0;
        }
        else
        {
            // An RTL line hangs off the left edge once it is wider than the field; its only scroll range is that overhang.
            float x0 = LineX0(_lines[0]);
            float min = Math.Min(0, x0);
            float max = _direction == TextDirection.Rtl ? 0 : Math.Max(0, _lines[0].Width - Size.Width + 2);
            float x = _state.ScrollX;
            if (caret.Dx < x) x = caret.Dx;
            else if (caret.Dx > x + Size.Width - 1) x = caret.Dx - Size.Width + 1;
            _state.ScrollX = Math.Clamp(x, min, max);
            _state.ScrollY = 0;
        }
    }

    /// <summary>Moves the caret one visual line up or down, keeping its x position.</summary>
    public void MoveVertical(int direction, bool extend)
    {
        if (_lines.Count == 0) return;
        int li = LineIndexOf(_state.Caret);
        int target = li + direction;
        if (target < 0) { _state.MoveTo(0, extend); AfterCaretMove(); return; }
        if (target >= _lines.Count) { _state.MoveTo(_state.Value.Length, extend); AfterCaretMove(); return; }
        float x = XOf(_lines[li], _state.Caret);
        _state.MoveTo(IndexAt(_lines[target], x), extend);
        AfterCaretMove();
    }

    /// <summary>
    /// Moves the caret one step left or right on screen. Left and right follow the glyphs, so in RTL text Left moves
    /// toward the logical end instead of the previous character. Word moves keep the logical order, mirrored for RTL.
    /// </summary>
    public void MoveVisualHorizontal(int direction, bool byWord, bool extend)
    {
        if (_lines.Count == 0) return;
        if (byWord)
        {
            _state.MoveHorizontal(_direction == TextDirection.Rtl ? -direction : direction, true, extend);
            AfterCaretMove();
            return;
        }

        if (!extend && _state.HasSelection)
        {
            // Collapse to the selection edge that is further in the arrow's direction.
            float a = CaretX(_state.SelectionStart), b = CaretX(_state.SelectionEnd);
            bool startIsLeft = a <= b;
            _state.MoveTo((direction < 0) == startIsLeft ? _state.SelectionStart : _state.SelectionEnd, false);
            AfterCaretMove();
            return;
        }

        int from = _state.Caret;
        var line = _lines[Math.Clamp(LineIndexOf(from), 0, _lines.Count - 1)];
        float x = XOf(line, from);
        const float eps = 0.01f;

        // The nearest grapheme boundary on this line that lies further in the arrow's direction.
        int target = -1;
        float targetX = 0;
        for (int i = line.Start; i <= line.End; i = _state.NextBoundary(i))
        {
            if (i != from)
            {
                float xi = XOf(line, i);
                bool better = direction < 0
                    ? xi < x - eps && (target < 0 || xi > targetX)
                    : xi > x + eps && (target < 0 || xi < targetX);
                if (better) { target = i; targetX = xi; }
            }
            if (i >= line.End || _state.NextBoundary(i) > line.End) break;
        }

        // At the end of the line, fall back to moving through the text.
        if (target < 0)
        {
            _state.MoveHorizontal(_direction == TextDirection.Rtl ? -direction : direction, false, extend);
            AfterCaretMove();
            return;
        }
        _state.MoveTo(target, extend);
        AfterCaretMove();
    }

    float CaretX(int index) => XOf(_lines[Math.Clamp(LineIndexOf(index), 0, _lines.Count - 1)], index);

    public void MoveToLineEdge(bool end, bool extend)
    {
        var l = _lines[Math.Clamp(LineIndexOf(_state.Caret), 0, _lines.Count - 1)];
        _state.MoveTo(end ? l.End : l.Start, extend);
        AfterCaretMove();
    }

    public int VisibleLineCount => Math.Max(1, (int)(Size.Height / Math.Max(1, _lineHeight)));

    void AfterCaretMove()
    {
        EnsureCaretVisible();
        MarkNeedsPaint();
        OnSelectionChanged?.Invoke();
    }

    // ---- painting ----

    public override void Paint(PaintingContext context, Offset offset)
    {
        var canvas = context.Canvas;
        canvas.Save();
        canvas.ClipRect(Size.ToRect(offset).ToSk());
        canvas.Translate(offset.Dx - _state.ScrollX, offset.Dy - _state.ScrollY);

        var font = Font;
        string text = Display;
        var color = _style.Color ?? SKColors.Black;

        if (text.Length == 0 && _hint is not null)
        {
            using var hp = new SKPaint { Color = _hintStyle.Color ?? SKColors.Gray, IsAntialias = true };
            var hfont = _hintStyle.ToFont();
            float hw = RenderParagraph.Measure(_hint, _hintStyle, hfont);
            float hx = _direction == TextDirection.Rtl ? Size.Width - hw : 0;
            RenderParagraph.DrawText(canvas, _hint, hx + _state.ScrollX, _baseline + _state.ScrollY, hfont, hp, null, _direction);
        }

        using var selPaint = new SKPaint { Color = _selectionColor, IsAntialias = true };
        using var textPaint = new SKPaint { Color = color, IsAntialias = true };
        for (int i = 0; i < _lines.Count; i++)
        {
            var l = _lines[i];
            float top = i * _lineHeight;
            if (top + _lineHeight < _state.ScrollY || top > _state.ScrollY + Size.Height) continue;

            if (_state.HasSelection && _focusedOrAlways)
            {
                int s = Math.Max(_state.SelectionStart, l.Start), e = Math.Min(_state.SelectionEnd, l.End);
                bool endsInSelection = _state.SelectionEnd > l.End && _state.SelectionStart <= l.End && l.HardBreak;
                if (s < e || endsInSelection)
                {
                    // One rectangle per bidi run, so a selection across directions highlights exactly the selected glyphs.
                    foreach (var run in l.Runs)
                    {
                        int rs = Math.Max(s, l.Start + run.Start), re = Math.Min(e, l.Start + run.Start + run.Length);
                        if (rs >= re) continue;
                        float x1 = LineX0(l) + run.Origin + run.Local[rs - l.Start - run.Start];
                        float x2 = LineX0(l) + run.Origin + run.Local[re - l.Start - run.Start];
                        canvas.DrawRect(Math.Min(x1, x2), top, Math.Abs(x2 - x1), _lineHeight, selPaint);
                    }
                    if (endsInSelection)
                    {
                        float tail = XOf(l, l.End); // a little tail for selected newlines
                        canvas.DrawRect(tail, top, 4, _lineHeight, selPaint);
                    }
                }
            }

            string s2 = text.Substring(l.Start, l.End - l.Start);
            RenderParagraph.DrawText(canvas, s2, LineX0(l), top + _baseline, font, textPaint, _style.LetterSpacing, _direction);
        }

        if (_focused && _caretVisible && !_state.HasSelection && _lines.Count > 0)
        {
            var c = CaretPosition(_state.Caret);
            using var caretPaint = new SKPaint { Color = _cursorColor, IsAntialias = false };
            // A caret at the very edge of the box (an empty RTL field) would fall outside the clip and vanish.
            float cx = Math.Clamp(c.Dx, _state.ScrollX, _state.ScrollX + Size.Width - 1.5f);
            canvas.DrawRect(cx, c.Dy + 1, 1.5f, _lineHeight - 2, caretPaint);
        }

        canvas.Restore();
    }

    bool _focusedOrAlways => true;

    // ---- pointer ----

    protected override bool HitTestSelf(Offset position) => true;

    int IndexAtPoint(Offset local)
    {
        if (_lines.Count == 0) return 0;
        float y = local.Dy + _state.ScrollY;
        int li = Math.Clamp((int)(y / Math.Max(1, _lineHeight)), 0, _lines.Count - 1);
        return IndexAt(_lines[li], local.Dx + _state.ScrollX);
    }

    public override void HandlePointerEvent(PointerEvent e, HitTestEntry entry)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Down:
            {
                RequestFocus?.Invoke();
                long t = e.TimestampMs;
                bool near = (e.LocalPosition - _lastDownPos).Distance < 5;
                _clickCount = t - _lastDownMs < 450 && near ? _clickCount + 1 : 1;
                _lastDownMs = t;
                _lastDownPos = e.LocalPosition;
                _dragging = true;
                _claimed = false;
                Arena.Add(e.Pointer, _arenaMember);

                int idx = IndexAtPoint(e.LocalPosition);
                if (_clickCount == 2) _state.SelectWordAt(idx);
                else if (_clickCount >= 3) { if (Multiline) _state.SelectLineAt(idx); else _state.SelectAll(); }
                else _state.MoveTo(idx, WidgetsBinding.Instance.Shift);
                AfterCaretMove();
                break;
            }
            case PointerEventKind.Move when _dragging && _clickCount == 1:
                // Selecting by dragging owns the pointer, so an enclosing scrollable does not scroll along with it.
                if (!_claimed) { _claimed = true; Arena.Resolve(e.Pointer, _arenaMember, true); }
                _state.MoveTo(IndexAtPoint(e.LocalPosition), true);
                AfterCaretMove();
                break;
            case PointerEventKind.Up:
                if (_dragging && !_claimed) Arena.Resolve(e.Pointer, _arenaMember, false); // a plain click stays available to ancestors
                _dragging = false;
                break;
        }
    }

    bool _claimed;
    readonly ArenaMember _arenaMember = new();

    sealed class ArenaMember : IGestureArenaMember
    {
        public void AcceptGesture(int pointer) { }
        public void RejectGesture(int pointer) { }
    }

    static GestureArena Arena => WidgetsBinding.Instance.Gestures.Arena;

    public override float? GetDistanceToBaseline() => _baseline;
    public override float MinIntrinsicWidth(float h) => 0;
    public override float MaxIntrinsicWidth(float h) => 200;
    public override float MinIntrinsicHeight(float w) => _lineHeight;
    public override float MaxIntrinsicHeight(float w) => Math.Max(1, _lines.Count) * _lineHeight;
}

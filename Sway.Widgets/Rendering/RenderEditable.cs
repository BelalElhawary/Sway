using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Lays out, paints and hit-tests a <see cref="TextEditState"/> (caret, selection, wrapping, inner scroll).</summary>
public sealed class RenderEditable : RenderBox
{
    sealed record Line(int Start, int End, bool HardBreak, float Width, bool Rtl);

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
            string s = text.Substring(start, end - start);
            _lines.Add(new Line(start, end, hard, RenderParagraph.Measure(s, _style, font), IsRtl(s)));
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
        float Measure(int end) => RenderParagraph.Measure(text.Substring(start, end - start), _style, font);
        if (Measure(hardEnd) <= width) return hardEnd;

        int lastSpace = -1;
        int i = start;
        for (; i < hardEnd; i = _state.NextBoundary(i))
        {
            int next = _state.NextBoundary(i);
            if (next > hardEnd) next = hardEnd;
            if (Measure(next) > width && i > start)
                break;
            if (text[i] == ' ') lastSpace = i + 1;
        }
        if (lastSpace > start && lastSpace <= i) return lastSpace;
        return Math.Max(i, _state.NextBoundary(start));
    }

    static bool IsRtl(string s) => TextShaper.IsRtlText(s);

    // ---- geometry ----

    int LineIndexOf(int index) => _state.LineOfIndex(index);

    float LineX0(Line l) => l.Rtl ? Size.Width - l.Width : 0;

    float XOf(Line l, int index)
    {
        string text = Display;
        index = Math.Clamp(index, l.Start, l.End);
        float w = RenderParagraph.Measure(text.Substring(l.Start, index - l.Start), _style, Font);
        return l.Rtl ? LineX0(l) + l.Width - w : LineX0(l) + w;
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
            float content = _lines[0].Width;
            float max = Math.Max(0, content - Size.Width + 2);
            float x = _state.ScrollX;
            if (caret.Dx < x) x = caret.Dx;
            else if (caret.Dx > x + Size.Width - 1) x = caret.Dx - Size.Width + 1;
            // Right-aligned (RTL) single lines never scroll; they simply overflow on the left.
            _state.ScrollX = _lines[0].Rtl ? 0 : Math.Clamp(x, 0, max);
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
                    float x1 = XOf(l, s), x2 = s < e ? XOf(l, e) : XOf(l, l.End) + 4; // a little tail for selected newlines
                    canvas.DrawRect(Math.Min(x1, x2), top, Math.Abs(x2 - x1), _lineHeight, selPaint);
                }
            }

            string s2 = text.Substring(l.Start, l.End - l.Start);
            RenderParagraph.DrawText(canvas, s2, LineX0(l), top + _baseline, font, textPaint, _style.LetterSpacing,
                l.Rtl ? TextDirection.Rtl : TextDirection.Ltr);
        }

        if (_focused && _caretVisible && !_state.HasSelection && _lines.Count > 0)
        {
            var c = CaretPosition(_state.Caret);
            using var caretPaint = new SKPaint { Color = _cursorColor, IsAntialias = false };
            canvas.DrawRect(c.Dx, c.Dy + 1, 1.5f, _lineHeight - 2, caretPaint);
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

                int idx = IndexAtPoint(e.LocalPosition);
                if (_clickCount == 2) _state.SelectWordAt(idx);
                else if (_clickCount >= 3) { if (Multiline) _state.SelectLineAt(idx); else _state.SelectAll(); }
                else _state.MoveTo(idx, WidgetsBinding.Instance.Shift);
                AfterCaretMove();
                break;
            }
            case PointerEventKind.Move when _dragging && _clickCount == 1:
                _state.MoveTo(IndexAtPoint(e.LocalPosition), true);
                AfterCaretMove();
                break;
            case PointerEventKind.Up:
                _dragging = false;
                break;
        }
    }

    public override float? GetDistanceToBaseline() => _baseline;
    public override float MinIntrinsicWidth(float h) => 0;
    public override float MaxIntrinsicWidth(float h) => 200;
    public override float MinIntrinsicHeight(float w) => _lineHeight;
    public override float MaxIntrinsicHeight(float w) => Math.Max(1, _lines.Count) * _lineHeight;
}

using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>
/// Block and inline layout. Block children stack vertically; runs of inline content are wrapped
/// into line boxes. Flex and grid come next, so display:flex currently lays out as a block.
/// Known gaps: no margin collapsing, inline elements have no box of their own.
/// </summary>
/// <summary>The inputs a block layout depends on besides the element's own subtree.</summary>
internal readonly record struct LayoutKey(float ContainingWidth, bool Shrink, float? ForcedWidth, float? ForcedHeight);

public sealed partial class LayoutEngine
{
    /// <summary>Reuse the previous layout of subtrees that did not change. Disabled by tests that compare against a full layout.</summary>
    public bool CacheEnabled { get; set; } = true;

    int _epoch;

    public void Layout(Document document, float viewportWidth, float viewportHeight)
    {
        var root = document.Root;
        _widthCache.Clear();
        _heightCache.Clear();
        _pending.Clear();
        _epoch = document.LayoutEpoch;
        root.ExtentsDirty = true; // the root's scroll limits depend on the viewport size
        _viewportWidth = document.ViewportWidth = viewportWidth;
        _viewportHeight = document.ViewportHeight = viewportHeight;

        LayoutBlock(root, 0, 0, viewportWidth, shrink: false);
        ComputeExtents(root);
        document.LayoutDirty = false;
    }

    static bool IsBlockLevel(Node node) =>
        node is ElementNode { Style.Display: Display.Block or Display.Flex or Display.Grid };

    static bool IsHidden(Node node) =>
        node is ElementNode { Style.Display: Display.None };

    // ---- block layout ----

    /// <summary>
    /// Lays out a block box whose margin box starts at (x, y). Returns the margin-box height.
    /// Flex and grid containers pass forced border-box sizes for the items they have already sized.
    /// </summary>
    float LayoutBlock(ElementNode el, float x, float y, float containingWidth, bool shrink,
        float? forcedWidth = null, float? forcedHeight = null)
    {
        // A subtree that did not change, laid out with the same inputs as last time, only needs to move.
        var key = new LayoutKey(containingWidth, shrink, forcedWidth, forcedHeight);
        if (CacheEnabled && !el.SubtreeLayoutDirty && !el.ContainsOutOfFlow && el.CacheEpoch == _epoch && el.CacheKey == key)
        {
            if (x != el.CacheX || y != el.CacheY) Translate(el, x - el.CacheX, y - el.CacheY);
            return el.CacheMarginHeight;
        }

        var st = el.Style;
        float mt = st.Margin[ComputedStyle.Top].Resolve(containingWidth) ?? 0;
        float mb = st.Margin[ComputedStyle.Bottom].Resolve(containingWidth) ?? 0;
        float? mlSet = st.Margin[ComputedStyle.Left].Resolve(containingWidth);
        float? mrSet = st.Margin[ComputedStyle.Right].Resolve(containingWidth);
        float pl = st.Padding[ComputedStyle.Left].Resolve(containingWidth) ?? 0;
        float pr = st.Padding[ComputedStyle.Right].Resolve(containingWidth) ?? 0;
        float pt = st.Padding[ComputedStyle.Top].Resolve(containingWidth) ?? 0;
        float pb = st.Padding[ComputedStyle.Bottom].Resolve(containingWidth) ?? 0;
        float bl = st.BorderWidth[ComputedStyle.Left], br = st.BorderWidth[ComputedStyle.Right];
        float bt = st.BorderWidth[ComputedStyle.Top], bb = st.BorderWidth[ComputedStyle.Bottom];

        float horizontalChrome = pl + pr + bl + br;
        float verticalChrome = pt + pb + bt + bb;

        float? specifiedWidth = st.Width.Resolve(containingWidth);
        if (specifiedWidth is { } sw && st.BorderBox) specifiedWidth = Math.Max(0, sw - horizontalChrome);
        if (forcedWidth is { } fw) specifiedWidth = Math.Max(0, fw - horizontalChrome);

        float ml = mlSet ?? 0, mr = mrSet ?? 0;
        float contentWidth;
        if (specifiedWidth is { } w)
        {
            contentWidth = w;
            if (mlSet is null && mrSet is null && forcedWidth is null)
            {
                float free = Math.Max(0, containingWidth - contentWidth - horizontalChrome);
                ml = mr = free / 2;
            }
        }
        else if (shrink || Controls.IsReplaced(el))
        {
            // Form controls size to their intrinsic width even when block-level.
            contentWidth = Math.Min(MaxContentWidth(el), Math.Max(0, containingWidth - ml - mr - horizontalChrome));
        }
        else
        {
            contentWidth = Math.Max(0, containingWidth - ml - mr - horizontalChrome);
        }

        if (forcedWidth is null)
            contentWidth = Clamp(contentWidth, st.MinWidth, st.MaxWidth, containingWidth, st.BorderBox ? horizontalChrome : 0);

        float borderX = x + ml;
        float borderY = y + mt;
        float contentX = borderX + bl + pl;
        float contentY = borderY + bt + pt;

        float? definiteHeight = null;
        if (forcedHeight is { } fh) definiteHeight = Math.Max(0, fh - verticalChrome);
        else if (st.Height.Unit == LengthUnit.Px)
            definiteHeight = st.BorderBox ? Math.Max(0, st.Height.Value - verticalChrome) : st.Height.Value;

        float contentHeight = LayoutChildren(el, contentX, contentY, contentWidth, definiteHeight);

        if (definiteHeight is { } dh) contentHeight = dh;
        if (forcedHeight is null)
            contentHeight = Clamp(contentHeight, st.MinHeight, st.MaxHeight, 0, st.BorderBox ? verticalChrome : 0);

        el.ContentRect = new SKRect(contentX, contentY, contentX + contentWidth, contentY + contentHeight);
        el.BorderRect = new SKRect(borderX, borderY,
            borderX + bl + pl + contentWidth + pr + br,
            borderY + bt + pt + contentHeight + pb + bb);

        ApplyRelativeOffset(el, containingWidth);
        // Absolute boxes anchored to this element can be placed now that its size is final.
        if (el.Parent is null || st.Position != Position.Static) LayoutPendingFor(el);

        float result = mt + el.BorderRect.Height + mb;
        el.CacheKey = key;
        el.CacheX = x;
        el.CacheY = y;
        el.CacheMarginHeight = result;
        el.CacheEpoch = _epoch;
        el.SubtreeLayoutDirty = false;
        return result;
    }

    static float Clamp(float value, Length min, Length max, float containing, float chrome)
    {
        if (min.Resolve(containing) is { } lo) value = Math.Max(value, lo - chrome);
        if (max.Resolve(containing) is { } hi) value = Math.Min(value, hi - chrome);
        return Math.Max(0, value);
    }

    /// <summary>Lays out the children of a block container and returns the content height.</summary>
    float LayoutChildren(ElementNode el, float x, float y, float width, float? definiteHeight)
    {
        // Controls draw their own contents; child nodes (options, textarea text) are data, not boxes.
        if (Controls.IsReplaced(el))
        {
            foreach (var child in el.PhysicalChildren) ClearText(child);
            return TextControls.IntrinsicContentHeight(el);
        }

        switch (el.Style.Display)
        {
            case Display.Flex or Display.InlineFlex:
                RegisterOutOfFlowChildren(el, x, y);
                return LayoutFlex(el, x, y, width, definiteHeight);
            case Display.Grid or Display.InlineGrid:
                RegisterOutOfFlowChildren(el, x, y);
                return LayoutGrid(el, x, y, width, definiteHeight);
        }

        var children = el.PhysicalChildren;
        float cursor = y;
        int i = 0;

        while (i < children.Count)
        {
            var child = children[i];
            if (IsHidden(child)) { ClearText(child); i++; continue; }

            if (IsOutOfFlow(child))
            {
                RegisterOutOfFlow((ElementNode)child, x, cursor);
                i++;
                continue;
            }

            if (IsBlockLevel(child))
            {
                cursor += LayoutBlock((ElementNode)child, x, cursor, width, shrink: false);
                i++;
                continue;
            }

            int start = i;
            while (i < children.Count && !IsBlockLevel(children[i]) && !IsOutOfFlow(children[i])) i++;
            cursor += LayoutInline(el, children, start, i, x, cursor, width);
        }
        return cursor - y;
    }

    static void ClearText(Node node)
    {
        if (node is TextNode t) t.Runs.Clear();
    }

    // ---- inline layout ----

    sealed class InlineItem
    {
        public TextNode? Text;
        public string Word = "";
        public bool IsSpace;
        public ComputedStyle Style = null!;
        public ElementNode? Atom;
        public float Width;     // advance width (text) or margin-box width (atom)
        public float Height;    // line-box contribution
        public float Ascent;    // text only: baseline offset from the top of the item's box
    }

    float _inlineOriginX, _inlineOriginY;

    sealed class Line
    {
        public readonly List<InlineItem> Items = new();
        public readonly List<float> Xs = new();
        public float Width;
    }

    float LayoutInline(ElementNode container, IReadOnlyList<Node> nodes, int start, int end, float x, float y, float width)
    {
        var items = new List<InlineItem>();
        _inlineOriginX = x;
        _inlineOriginY = y;
        bool previousWasSpace = true; // collapses leading whitespace
        for (int i = start; i < end; i++) Collect(nodes[i], items, ref previousWasSpace, width);

        var lines = new List<Line>();
        var line = new Line();
        foreach (var item in items)
        {
            if (item.IsSpace && line.Items.Count == 0) continue; // no space at the start of a line

            if (!item.IsSpace && line.Width + item.Width > width && line.Items.Count > 0)
            {
                TrimTrailingSpace(line);
                lines.Add(line);
                line = new Line();
            }

            line.Items.Add(item);
            line.Xs.Add(line.Width);
            line.Width += item.Width;
        }
        TrimTrailingSpace(line);
        if (line.Items.Count > 0) lines.Add(line);

        float cursorY = y;
        var align = container.Style.TextAlign;
        foreach (var l in lines)
        {
            float lineHeight = l.Items.Max(it => it.Height);
            float offset = align switch
            {
                TextAlign.Center => (width - l.Width) / 2,
                TextAlign.Right => width - l.Width,
                _ => 0
            };
            offset = Math.Max(0, offset);

            for (int i = 0; i < l.Items.Count; i++)
            {
                var item = l.Items[i];
                float itemX = x + offset + l.Xs[i];

                if (item.Atom is { } atom)
                {
                    float top = cursorY + (lineHeight - item.Height) / 2;
                    // Atoms are measured at the origin first, so move them into place.
                    var margin = atom.Style.Margin;
                    float ml = margin[ComputedStyle.Left].Resolve(width) ?? 0;
                    float mt = margin[ComputedStyle.Top].Resolve(width) ?? 0;
                    float dx = itemX + ml - (atom.BorderRect.Left - atom.RelativeOffset.X);
                    float dy = top + mt - (atom.BorderRect.Top - atom.RelativeOffset.Y);
                    Translate(atom, dx, dy);
                }
                else if (item.Text is { } text)
                {
                    float contentArea = FontCache.Get(item.Style).Metrics.Descent - FontCache.Get(item.Style).Metrics.Ascent;
                    float top = cursorY + (lineHeight - item.Height) / 2;
                    float baseline = top + (item.Height - contentArea) / 2 + (-FontCache.Get(item.Style).Metrics.Ascent);
                    AppendRun(text, item.Word, itemX, baseline, item.Width);
                }
            }
            cursorY += lineHeight;
        }

        return cursorY - y;
    }

    static void AppendRun(TextNode node, string word, float x, float baseline, float width)
    {
        // Merge with the previous run when it is the same line and directly adjacent, to keep run counts low.
        var runs = node.Runs;
        if (runs.Count > 0)
        {
            var last = runs[^1];
            if (Math.Abs(last.Baseline - baseline) < 0.01f && Math.Abs(last.X + last.Width - x) < 0.5f)
            {
                runs[^1] = last with { Text = last.Text + word, Width = last.Width + width };
                return;
            }
        }
        runs.Add(new TextRun(word, x, baseline, width));
    }

    static void TrimTrailingSpace(Line line)
    {
        while (line.Items.Count > 0 && line.Items[^1].IsSpace)
        {
            line.Width -= line.Items[^1].Width;
            line.Items.RemoveAt(line.Items.Count - 1);
            line.Xs.RemoveAt(line.Xs.Count - 1);
        }
    }

    void Collect(Node node, List<InlineItem> items, ref bool previousWasSpace, float availableWidth)
    {
        switch (node)
        {
            case TextNode text:
                CollectText(text, text.ParentElement!.Style, items, ref previousWasSpace);
                break;

            case ElementNode { Style.Display: Display.Inline } inline:
                inline.BorderRect = default;
                inline.ContentRect = default;
                foreach (var child in inline.PhysicalChildren)
                {
                    if (IsHidden(child)) { ClearText(child); continue; }
                    Collect(child, items, ref previousWasSpace, availableWidth);
                }
                break;

            case ElementNode { Style.Display: Display.None } hidden:
                ClearText(hidden);
                break;

            case ElementNode outOfFlow when outOfFlow.Style.IsOutOfFlow:
                RegisterOutOfFlow(outOfFlow, _inlineOriginX, _inlineOriginY);
                break;

            case ElementNode atom: // inline-block, or a block nested inside inline content
            {
                LayoutBlock(atom, 0, 0, availableWidth, shrink: true);
                var m = atom.Style.Margin;
                float ml = m[ComputedStyle.Left].Resolve(availableWidth) ?? 0;
                float mr = m[ComputedStyle.Right].Resolve(availableWidth) ?? 0;
                float mt = m[ComputedStyle.Top].Resolve(availableWidth) ?? 0;
                float mb = m[ComputedStyle.Bottom].Resolve(availableWidth) ?? 0;
                items.Add(new InlineItem
                {
                    Atom = atom,
                    Style = atom.Style,
                    Width = ml + atom.BorderRect.Width + mr,
                    Height = mt + atom.BorderRect.Height + mb,
                });
                previousWasSpace = false;
                break;
            }
        }
    }

    static void CollectText(TextNode node, ComputedStyle style, List<InlineItem> items, ref bool previousWasSpace)
    {
        node.Runs.Clear();
        string text = node.Text;
        int i = 0;
        float lineHeight = FontCache.LineHeight(style);
        var metrics = FontCache.Get(style).Metrics;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (previousWasSpace) continue;
                previousWasSpace = true;
                items.Add(new InlineItem
                {
                    Text = node, Word = " ", IsSpace = true, Style = style,
                    Width = FontCache.Measure(" ", style), Height = lineHeight, Ascent = -metrics.Ascent,
                });
                continue;
            }

            int wordStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            string word = text[wordStart..i];
            previousWasSpace = false;
            items.Add(new InlineItem
            {
                Text = node, Word = word, Style = style,
                Width = FontCache.Measure(word, style), Height = lineHeight, Ascent = -metrics.Ascent,
            });
        }
    }

    // ---- intrinsic sizing ----

    /// <summary>Content-box width needed to lay the contents out on a single line.</summary>
    float MaxContentWidth(ElementNode el) => ContentWidth(el, min: false);

    /// <summary>Content-box width of an element at max-content, or at min-content (its narrowest unbreakable width).</summary>
    float ContentWidth(ElementNode el, bool min)
    {
        if (Controls.IsReplaced(el)) return TextControls.IntrinsicContentWidth(el);

        switch (el.Style.Display)
        {
            case Display.Flex or Display.InlineFlex: return FlexContentWidth(el, min);
            case Display.Grid or Display.InlineGrid: return GridContentWidth(el, min);
        }

        float widest = 0, run = 0;
        bool previousWasSpace = true;

        void FlushRun() { widest = Math.Max(widest, run); run = 0; previousWasSpace = true; }

        void Walk(Node node)
        {
            switch (node)
            {
                case TextNode t:
                {
                    var style = t.ParentElement!.Style;
                    foreach (var piece in SplitCollapsed(t.Text, ref previousWasSpace))
                    {
                        if (!min) { run += FontCache.Measure(piece, style); continue; }
                        // Min-content breaks at every space, so only the widest word counts.
                        if (piece == " ") FlushRun();
                        else widest = Math.Max(widest, FontCache.Measure(piece, style));
                    }
                    break;
                }
                case ElementNode { Style.Display: Display.None }:
                    break;
                case ElementNode { Style.IsOutOfFlow: true }:
                    break;
                case ElementNode { Style.Display: Display.Inline } inline:
                    foreach (var child in inline.PhysicalChildren) Walk(child);
                    break;
                case ElementNode block when IsBlockLevel(block):
                    FlushRun();
                    widest = Math.Max(widest, OuterIntrinsicWidth(block, min));
                    break;
                case ElementNode atom:
                    if (min) widest = Math.Max(widest, OuterIntrinsicWidth(atom, min));
                    else run += OuterIntrinsicWidth(atom, min);
                    previousWasSpace = false;
                    break;
            }
        }

        foreach (var child in el.PhysicalChildren) Walk(child);
        FlushRun();
        return widest;
    }

    /// <summary>Border-box width at max-content or min-content, honouring an explicit width.</summary>
    float IntrinsicBorderWidth(ElementNode el, bool min)
    {
        if (_widthCache.TryGetValue((el, min), out var cached)) return cached;
        var st = el.Style;
        float chrome = HorizontalChrome(st);

        float content = st.Width.Unit == LengthUnit.Px
            ? (st.BorderBox ? Math.Max(0, st.Width.Value - chrome) : st.Width.Value)
            : ContentWidth(el, min);

        if (st.MinWidth.Unit == LengthUnit.Px) content = Math.Max(content, st.BorderBox ? st.MinWidth.Value - chrome : st.MinWidth.Value);
        if (st.MaxWidth.Unit == LengthUnit.Px) content = Math.Min(content, st.BorderBox ? st.MaxWidth.Value - chrome : st.MaxWidth.Value);
        return _widthCache[(el, min)] = Math.Max(0, content) + chrome;
    }

    float OuterIntrinsicWidth(ElementNode el, bool min)
    {
        var st = el.Style;
        float margin = (st.Margin[ComputedStyle.Left].Resolve(0) ?? 0) + (st.Margin[ComputedStyle.Right].Resolve(0) ?? 0);
        return IntrinsicBorderWidth(el, min) + margin;
    }

    // Percentages resolve against zero here: intrinsic sizing has no containing block yet.
    static float HorizontalChrome(ComputedStyle st) =>
        (st.Padding[ComputedStyle.Left].Resolve(0) ?? 0) + (st.Padding[ComputedStyle.Right].Resolve(0) ?? 0)
        + st.BorderWidth[ComputedStyle.Left] + st.BorderWidth[ComputedStyle.Right];

    static float VerticalChrome(ComputedStyle st, float containingWidth) =>
        (st.Padding[ComputedStyle.Top].Resolve(containingWidth) ?? 0) + (st.Padding[ComputedStyle.Bottom].Resolve(containingWidth) ?? 0)
        + st.BorderWidth[ComputedStyle.Top] + st.BorderWidth[ComputedStyle.Bottom];

    static IEnumerable<string> SplitCollapsed(string text, ref bool previousWasSpace)
    {
        var pieces = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (!previousWasSpace) { pieces.Add(" "); previousWasSpace = true; }
            }
            else
            {
                int s = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                pieces.Add(text[s..i]);
                previousWasSpace = false;
            }
        }
        return pieces;
    }

    // ---- helpers ----

    static void Translate(ElementNode el, float dx, float dy)
    {
        el.BorderRect = Offset(el.BorderRect, dx, dy);
        el.ContentRect = Offset(el.ContentRect, dx, dy);
        // Cached layouts remember the origin they were laid out at, so moving a subtree moves that too.
        el.CacheX += dx;
        el.CacheY += dy;
        if (el.HasVisualBounds) el.VisualBounds = Offset(el.VisualBounds, dx, dy);

        foreach (var child in el.PhysicalChildren)
        {
            if (child is ElementNode e) Translate(e, dx, dy);
            else if (child is TextNode t)
                for (int i = 0; i < t.Runs.Count; i++)
                {
                    var r = t.Runs[i];
                    t.Runs[i] = r with { X = r.X + dx, Baseline = r.Baseline + dy };
                }
        }
    }

    static SKRect Offset(SKRect r, float dx, float dy) => new(r.Left + dx, r.Top + dy, r.Right + dx, r.Bottom + dy);
}

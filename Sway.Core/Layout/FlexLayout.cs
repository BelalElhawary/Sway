using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>
/// Flexbox following the CSS Flexible Box algorithm: flex base sizes, line breaking, resolving
/// flexible lengths with min/max freezing, cross sizing, then alignment. Baseline alignment and
/// wrap-reverse are treated as flex-start for now.
/// </summary>
public sealed partial class LayoutEngine
{
    sealed class FlexItem
    {
        public required LayoutItem Item { get; init; }
        public float Grow, Shrink;
        public float Base, Hypothetical, Min, Max = float.PositiveInfinity, Target;
        public bool Frozen;
        public Align Align;
        public bool CrossAuto;       // cross size is auto, so stretch may apply
        public float Cross;          // border-box cross size
        public float MainStart, CrossStart; // border-box position inside the container
    }

    float LayoutFlex(ElementNode el, float x, float y, float cw, float? definiteHeight)
    {
        var st = el.Style;
        bool isRtl = st.Direction == Direction.Rtl;
        bool row = st.FlexDirection is FlexDirection.Row or FlexDirection.RowReverse;
        bool reverse = row ? (isRtl ? st.FlexDirection == FlexDirection.Row : st.FlexDirection == FlexDirection.RowReverse)
                           : st.FlexDirection == FlexDirection.ColumnReverse;
        float mainGap = row ? st.ColumnGap : st.RowGap;
        float crossGap = row ? st.RowGap : st.ColumnGap;
        int mainStartSide = row ? (isRtl ? R : L) : T;
        int mainEndSide = row ? (isRtl ? L : R) : B;
        int crossStartSide = row ? T : (isRtl ? R : L);
        int crossEndSide = row ? B : (isRtl ? L : R);

        var items = CollectItems(el, cw).Select(i => new FlexItem { Item = i }).ToList();
        if (items.Count == 0) return 0;

        float? availMain = row ? cw : definiteHeight;
        float? availCross = row ? definiteHeight : cw;

        float MainMargins(FlexItem f) => f.Item.Margin[mainStartSide] + f.Item.Margin[mainEndSide];
        float CrossMargins(FlexItem f) => f.Item.Margin[crossStartSide] + f.Item.Margin[crossEndSide];

        foreach (var f in items)
            ComputeFlexBase(f, st, row, cw, availMain, definiteHeight);

        // Collect items into lines.
        var lines = new List<List<FlexItem>>();
        if (!st.FlexWrap || availMain is null)
        {
            lines.Add(items);
        }
        else
        {
            var current = new List<FlexItem>();
            float used = 0;
            foreach (var f in items)
            {
                float outer = f.Hypothetical + MainMargins(f);
                if (current.Count > 0 && used + mainGap + outer > availMain + 0.01f)
                {
                    lines.Add(current);
                    current = new List<FlexItem>();
                    used = 0;
                }
                used += (current.Count > 0 ? mainGap : 0) + outer;
                current.Add(f);
            }
            lines.Add(current);
        }

        // Resolve flexible lengths line by line.
        foreach (var line in lines)
        {
            if (availMain is { } am) ResolveFlexibleLengths(line, am, mainGap, MainMargins);
            else foreach (var f in line) f.Target = f.Hypothetical;
        }

        // Cross sizes of each item, then of each line.
        foreach (var line in lines)
            foreach (var f in line)
                f.Cross = ComputeFlexCross(f, row, cw, definiteHeight);

        var lineCross = new float[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            lineCross[i] = lines[i].Max(f => f.Cross + CrossMargins(f));

        float crossStartOffset = 0, crossBetween = 0;
        if (lines.Count == 1 && availCross is { } singleCross)
        {
            lineCross[0] = singleCross;
        }
        else if (availCross is { } ac)
        {
            float free = ac - lineCross.Sum() - crossGap * (lines.Count - 1);
            if (st.AlignContent == Align.Stretch)
            {
                if (free > 0) for (int i = 0; i < lineCross.Length; i++) lineCross[i] += free / lines.Count;
            }
            else
            {
                (crossStartOffset, crossBetween) = Distribute(free, lines.Count, st.AlignContent);
            }
        }

        // Stretch auto-sized items to the line.
        for (int i = 0; i < lines.Count; i++)
            foreach (var f in lines[i])
                if (f.Align == Align.Stretch && f.CrossAuto
                    && !f.Item.AutoMargin[crossStartSide] && !f.Item.AutoMargin[crossEndSide])
                    f.Cross = Math.Max(0, lineCross[i] - CrossMargins(f));

        // Main-axis positions.
        float mainSize = availMain ?? lines.Max(l => l.Sum(f => f.Target + MainMargins(f)) + mainGap * (l.Count - 1));
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            float used = line.Sum(f => f.Target + MainMargins(f)) + mainGap * (line.Count - 1);
            float free = mainSize - used;

            int autoMargins = line.Sum(f => (f.Item.AutoMargin[mainStartSide] ? 1 : 0) + (f.Item.AutoMargin[mainEndSide] ? 1 : 0));
            float autoShare = 0, start = 0, between = 0;
            if (autoMargins > 0 && free > 0) autoShare = free / autoMargins;
            else (start, between) = Distribute(free, line.Count, st.JustifyContent);

            float cursor = start;
            foreach (var f in line)
            {
                float before = f.Item.AutoMargin[mainStartSide] ? autoShare : f.Item.Margin[mainStartSide];
                float after = f.Item.AutoMargin[mainEndSide] ? autoShare : f.Item.Margin[mainEndSide];
                f.MainStart = cursor + before;
                cursor += before + f.Target + after + mainGap + between;
            }

            // Reversed directions mirror the whole line inside the container.
            if (reverse) foreach (var f in line) f.MainStart = mainSize - f.MainStart - f.Target;
        }

        // Cross-axis positions.
        float lineOffset = crossStartOffset;
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (var f in lines[i])
            {
                float outer = f.Cross + CrossMargins(f);
                float free = lineCross[i] - outer;
                bool autoStart = f.Item.AutoMargin[crossStartSide], autoEnd = f.Item.AutoMargin[crossEndSide];

                // Auto margins absorb free space and take precedence over align-items.
                float offset = autoStart || autoEnd
                    ? Math.Max(0, autoStart ? (autoEnd ? free / 2 : free) : 0)
                    : f.Align switch
                    {
                        Align.End => (!row && isRtl) ? 0 : free,
                        Align.Start => (!row && isRtl) ? free : 0,
                        Align.Center => free / 2,
                        _ => (!row && isRtl && f.Align != Align.Stretch) ? free : 0
                    };

                f.CrossStart = lineOffset + offset + f.Item.Margin[crossStartSide];
            }
            lineOffset += lineCross[i] + crossGap + crossBetween;
        }

        // Final layout of every item at its resolved position and size.
        foreach (var line in lines)
        {
            foreach (var f in line)
            {
                float bx = x + (row ? f.MainStart : f.CrossStart);
                float by = y + (row ? f.CrossStart : f.MainStart);
                float w = row ? f.Target : f.Cross;
                float h = row ? f.Cross : f.Target;
                PlaceItem(f.Item, bx, by, w, h, cw);
            }
        }

        return row ? lineCross.Sum() + crossGap * (lines.Count - 1) : mainSize;
    }

    /// <summary>Computes the flex base size, hypothetical size and min/max clamps for one item (all border-box).</summary>
    void ComputeFlexBase(FlexItem f, ComputedStyle container, bool row, float cw, float? availMain, float? definiteHeight)
    {
        var it = f.Item;
        var st = it.Style;
        f.Grow = st.FlexGrow;
        f.Shrink = st.FlexShrink;
        f.Align = st.AlignSelf == Align.Auto ? container.AlignItems : st.AlignSelf;
        f.CrossAuto = row ? st.Height.IsAuto || st.Height.Unit == LengthUnit.Percent && definiteHeight is null
                          : st.Width.IsAuto;

        float hChrome = HorizontalChromeAt(st, cw);
        float vChrome = VerticalChrome(st, cw);
        float mainChrome = row ? hChrome : vChrome;
        int ms = row ? L : T, me = row ? R : B;
        float mainMargins = it.Margin[ms] + it.Margin[me];

        // Explicit size suggestion: flex-basis wins over width/height.
        float? specified;
        if (!st.FlexBasis.IsAuto)
        {
            specified = st.FlexBasis.Unit == LengthUnit.Percent
                ? availMain is { } am ? st.FlexBasis.Value / 100f * am : null
                : st.FlexBasis.Resolve(0);
            if (specified is { } s0 && !st.BorderBox) specified = s0 + mainChrome;
        }
        else
        {
            specified = row ? SpecifiedBorderWidth(it, cw) : SpecifiedBorderHeight(it, cw, availMain);
        }
        if (specified is { } s1) specified = Math.Max(s1, mainChrome);

        float crossWidthForColumn = row ? 0 : ColumnItemWidth(f, cw);

        f.Base = specified ?? (row
            ? IntrinsicWidth(it, min: false)
            : NaturalHeight(it, crossWidthForColumn, cw));

        // Min/max main size. An auto minimum keeps items from shrinking below their content.
        var minL = row ? st.MinWidth : st.MinHeight;
        var maxL = row ? st.MaxWidth : st.MaxHeight;
        float containing = row ? cw : availMain ?? 0;

        if (minL.Resolve(containing) is { } minV) f.Min = st.BorderBox ? minV : minV + mainChrome;
        else
        {
            bool scrolls = row ? st.OverflowX != Overflow.Visible : st.OverflowY != Overflow.Visible;
            float contentMin = scrolls ? 0 : row ? IntrinsicWidth(it, min: true) : NaturalHeight(it, crossWidthForColumn, cw);
            float? explicitSize = row ? SpecifiedBorderWidth(it, cw) : SpecifiedBorderHeight(it, cw, availMain);
            f.Min = explicitSize is { } es ? Math.Min(es, contentMin) : contentMin;
        }
        if (maxL.Resolve(containing) is { } maxV) f.Max = st.BorderBox ? maxV : maxV + mainChrome;

        f.Hypothetical = Math.Max(f.Min, Math.Min(f.Max, f.Base));
    }

    /// <summary>Width of an item in a column container before any line stretching.</summary>
    float ColumnItemWidth(FlexItem f, float cw)
    {
        var it = f.Item;
        float avail = Math.Max(0, cw - it.Margin[L] - it.Margin[R]);
        float width = SpecifiedBorderWidth(it, cw)
            ?? (f.Align == Align.Stretch && !it.AutoMargin[L] && !it.AutoMargin[R]
                ? avail
                : Math.Min(IntrinsicWidth(it, min: false), avail));
        return ClampBorderSize(width, HorizontalChromeAt(it.Style, cw), it.Style.MinWidth, it.Style.MaxWidth, cw, it.Style.BorderBox);
    }

    float ComputeFlexCross(FlexItem f, bool row, float cw, float? definiteHeight)
    {
        var it = f.Item;
        var st = it.Style;

        if (!row) return ColumnItemWidth(f, cw);

        float? specified = SpecifiedBorderHeight(it, cw, definiteHeight);
        float height = specified ?? NaturalHeight(it, f.Target, cw);
        return ClampBorderSize(height, VerticalChrome(st, cw), st.MinHeight, st.MaxHeight, definiteHeight ?? 0, st.BorderBox);
    }

    /// <summary>
    /// Distributes free space across a line, freezing items that hit their min or max and repeating
    /// until every item is settled (the "resolve flexible lengths" step).
    /// </summary>
    static void ResolveFlexibleLengths(List<FlexItem> line, float availMain, float gap, Func<FlexItem, float> mainMargins)
    {
        float gaps = gap * (line.Count - 1);
        float hypotheticalSum = line.Sum(f => f.Hypothetical + mainMargins(f)) + gaps;
        bool growing = hypotheticalSum < availMain;

        foreach (var f in line)
        {
            f.Frozen = (growing ? f.Grow == 0 : f.Shrink == 0)
                || (growing ? f.Base > f.Hypothetical : f.Base < f.Hypothetical);
            f.Target = f.Frozen ? f.Hypothetical : f.Base;
        }

        float initialFree = availMain - line.Sum(f => (f.Frozen ? f.Target : f.Base) + mainMargins(f)) - gaps;

        for (int pass = 0; pass <= line.Count; pass++)
        {
            var flexible = line.Where(f => !f.Frozen).ToList();
            if (flexible.Count == 0) break;

            float free = availMain - line.Sum(f => (f.Frozen ? f.Target : f.Base) + mainMargins(f)) - gaps;

            if (growing)
            {
                float totalGrow = flexible.Sum(f => f.Grow);
                // Factors summing to less than one only claim that fraction of the free space.
                if (totalGrow < 1 && Math.Abs(initialFree * totalGrow) < Math.Abs(free)) free = initialFree * totalGrow;
                foreach (var f in flexible)
                    f.Target = f.Base + (totalGrow > 0 ? free * f.Grow / totalGrow : 0);
            }
            else
            {
                float totalScaled = flexible.Sum(f => f.Shrink * f.Base);
                foreach (var f in flexible)
                    f.Target = f.Base + (totalScaled > 0 ? free * (f.Shrink * f.Base) / totalScaled : 0);
            }

            float violation = 0;
            var clamped = new Dictionary<FlexItem, float>();
            foreach (var f in flexible)
            {
                float c = Math.Max(f.Min, Math.Min(f.Max, f.Target));
                c = Math.Max(0, c);
                violation += c - f.Target;
                clamped[f] = c;
            }

            foreach (var f in flexible)
            {
                float c = clamped[f];
                if (violation == 0 || (violation > 0 && c > f.Target) || (violation < 0 && c < f.Target))
                {
                    f.Frozen = true;
                }
                f.Target = c;
            }
        }
    }

    float FlexContentWidth(ElementNode el, bool min)
    {
        var st = el.Style;
        bool row = st.FlexDirection is FlexDirection.Row or FlexDirection.RowReverse;
        var items = CollectItems(el, 0);
        if (items.Count == 0) return 0;

        var widths = items.Select(i => OuterIntrinsicWidth(i, min)).ToList();
        if (!row) return widths.Max();

        // Wrapping rows can break before every item, so their min-content is the widest item.
        if (min && st.FlexWrap) return widths.Max();
        return widths.Sum() + st.ColumnGap * (items.Count - 1);
    }
}

using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>
/// CSS grid: explicit and implicit tracks (px, %, fr, auto, min/max-content, minmax, repeat incl.
/// auto-fill), row-flow auto-placement with spans, gaps, and item alignment. Not yet supported:
/// named lines and areas, column auto-flow, dense packing, subgrid.
/// </summary>
public sealed partial class LayoutEngine
{
    sealed class GridItem
    {
        public required LayoutItem Item { get; init; }
        public int Row, Col, RowSpan = 1, ColSpan = 1;
    }

    readonly record struct Contribution(int Start, int Span, float Min, float Max);

    float LayoutGrid(ElementNode el, float x, float y, float cw, float? definiteHeight)
    {
        var st = el.Style;
        float colGap = st.ColumnGap, rowGap = st.RowGap;

        var colTracks = st.GridColumns.Resolve(cw, colGap);
        var rowTracks = st.GridRows.Resolve(definiteHeight, rowGap);
        var placed = PlaceGridItems(CollectItems(el, cw), colTracks.Count, rowTracks.Count, out int colCount, out int rowCount);

        while (colTracks.Count < colCount) colTracks.Add(st.GridAutoColumns);
        while (rowTracks.Count < rowCount) rowTracks.Add(st.GridAutoRows);
        if (colTracks.Count == 0) return 0;

        // Columns first: they depend only on item widths.
        var colContribs = placed.Select(g => new Contribution(g.Col, g.ColSpan,
            OuterIntrinsicWidth(g.Item, min: true), OuterIntrinsicWidth(g.Item, min: false))).ToList();
        float[] cols = SizeTracks(colTracks, cw, colGap, colContribs, st.JustifyContent == Align.Stretch);

        var (colStart, colBetween) = Distribute(cw - cols.Sum() - colGap * (cols.Length - 1), cols.Length, st.JustifyContent);
        var colX = TrackOffsets(cols, x + colStart, colGap + colBetween);

        // Rows depend on the heights items take at their column widths.
        var rowContribs = new List<Contribution>();
        foreach (var g in placed)
        {
            float areaWidth = SpanSize(cols, g.Col, g.ColSpan, colGap);
            float width = ResolveGridItemWidth(g.Item, areaWidth, GridJustify(st, g.Item), cw);
            float height = NaturalHeight(g.Item, width, cw) + g.Item.Margin[T] + g.Item.Margin[B];
            rowContribs.Add(new Contribution(g.Row, g.RowSpan, height, height));
        }
        float[] rows = SizeTracks(rowTracks, definiteHeight, rowGap, rowContribs,
            st.AlignContent == Align.Stretch && definiteHeight is not null);

        float rowStart = 0, rowBetween = 0;
        if (definiteHeight is { } dh)
            (rowStart, rowBetween) = Distribute(dh - rows.Sum() - rowGap * (rows.Length - 1), rows.Length, st.AlignContent);
        var rowY = TrackOffsets(rows, y + rowStart, rowGap + rowBetween);

        foreach (var g in placed)
        {
            var it = g.Item;
            float areaX = colX[g.Col], areaY = rowY[g.Row];
            float areaW = SpanSize(cols, g.Col, g.ColSpan, colGap + colBetween);
            float areaH = SpanSize(rows, g.Row, g.RowSpan, rowGap + rowBetween);

            var justify = GridJustify(st, it);
            var align = it.Style.AlignSelf == Align.Auto ? st.AlignItems : it.Style.AlignSelf;

            float width = ResolveGridItemWidth(it, areaW, justify, cw);
            float availH = Math.Max(0, areaH - it.Margin[T] - it.Margin[B]);
            float height = SpecifiedBorderHeight(it, cw, areaH)
                ?? (align == Align.Stretch ? availH : NaturalHeight(it, width, cw));
            height = ClampBorderSize(height, VerticalChrome(it.Style, cw), it.Style.MinHeight, it.Style.MaxHeight, areaH, it.Style.BorderBox);

            float freeX = areaW - it.Margin[L] - it.Margin[R] - width;
            float freeY = areaH - it.Margin[T] - it.Margin[B] - height;
            float dx = justify switch { Align.End => freeX, Align.Center => freeX / 2, _ => 0 };
            float dy = align switch { Align.End => freeY, Align.Center => freeY / 2, _ => 0 };

            PlaceItem(it, areaX + dx + it.Margin[L], areaY + dy + it.Margin[T], width, height, cw);
        }

        return rows.Sum() + rowGap * Math.Max(0, rows.Length - 1);
    }

    static Align GridJustify(ComputedStyle container, LayoutItem item) =>
        item.Style.JustifySelf == Align.Auto ? container.JustifyItems : item.Style.JustifySelf;

    float ResolveGridItemWidth(LayoutItem it, float areaWidth, Align justify, float cw)
    {
        float avail = Math.Max(0, areaWidth - it.Margin[L] - it.Margin[R]);
        float width = SpecifiedBorderWidth(it, cw)
            ?? (justify == Align.Stretch ? avail : Math.Min(IntrinsicWidth(it, min: false), avail));
        return ClampBorderSize(width, HorizontalChromeAt(it.Style, cw), it.Style.MinWidth, it.Style.MaxWidth, cw, it.Style.BorderBox);
    }

    static float SpanSize(float[] sizes, int start, int span, float gap)
    {
        float total = gap * (span - 1);
        for (int i = start; i < start + span && i < sizes.Length; i++) total += sizes[i];
        return total;
    }

    static float[] TrackOffsets(float[] sizes, float origin, float gapBetween)
    {
        var offsets = new float[sizes.Length];
        float cursor = origin;
        for (int i = 0; i < sizes.Length; i++)
        {
            offsets[i] = cursor;
            cursor += sizes[i] + gapBetween;
        }
        return offsets;
    }

    // ---- placement ----

    /// <summary>
    /// Converts start/end lines into a zero-based position (null when auto) and a span.
    /// Negative lines count back from the end of the explicit grid.
    /// </summary>
    static (int? pos, int span) ResolveAxis(GridLine start, GridLine end, int explicitCount)
    {
        int LineIndex(int n) => Math.Max(0, n > 0 ? n - 1 : explicitCount + n + 1);
        bool startFixed = !start.IsSpan && !start.IsAuto, endFixed = !end.IsSpan && !end.IsAuto;

        if (startFixed && endFixed)
        {
            int a = LineIndex(start.Value), b = LineIndex(end.Value);
            if (a > b) (a, b) = (b, a);
            return (a, Math.Max(1, b - a));
        }
        if (startFixed) return (LineIndex(start.Value), end.IsSpan ? end.Value : 1);
        if (endFixed)
        {
            int span = start.IsSpan ? start.Value : 1;
            return (Math.Max(0, LineIndex(end.Value) - span), span);
        }
        return (null, start.IsSpan ? start.Value : end.IsSpan ? end.Value : 1);
    }

    List<GridItem> PlaceGridItems(List<LayoutItem> items, int explicitCols, int explicitRows, out int colCount, out int rowCount)
    {
        var placed = new List<GridItem>();
        var occupied = new HashSet<(int row, int col)>();
        var pending = new List<(GridItem item, int? row, int? col)>();
        colCount = Math.Max(1, explicitCols);

        foreach (var it in items)
        {
            var (col, colSpan) = ResolveAxis(it.Style.ColumnStart, it.Style.ColumnEnd, explicitCols);
            var (row, rowSpan) = ResolveAxis(it.Style.RowStart, it.Style.RowEnd, explicitRows);
            var g = new GridItem { Item = it, ColSpan = colSpan, RowSpan = rowSpan };
            colCount = Math.Max(colCount, Math.Max(colSpan, (col ?? 0) + colSpan));
            pending.Add((g, row, col));
            placed.Add(g);
        }

        bool IsFree(int row, int col, int rowSpan, int colSpan)
        {
            for (int r = row; r < row + rowSpan; r++)
                for (int c = col; c < col + colSpan; c++)
                    if (occupied.Contains((r, c))) return false;
            return true;
        }

        void Occupy(GridItem g)
        {
            for (int r = g.Row; r < g.Row + g.RowSpan; r++)
                for (int c = g.Col; c < g.Col + g.ColSpan; c++)
                    occupied.Add((r, c));
        }

        // Items with a fixed row and column claim their cells first.
        foreach (var (g, row, col) in pending.Where(p => p.row is not null && p.col is not null))
        {
            g.Row = row!.Value;
            g.Col = col!.Value;
            Occupy(g);
        }

        // Items with a fixed row search along it for room.
        foreach (var (g, row, col) in pending.Where(p => p.row is not null && p.col is null))
        {
            g.Row = row!.Value;
            int c = 0;
            while (!IsFree(g.Row, c, g.RowSpan, g.ColSpan)) c++;
            g.Col = c;
            colCount = Math.Max(colCount, c + g.ColSpan);
            Occupy(g);
        }

        // Everything else flows in document order with a cursor (row-major, sparse packing).
        int cursorRow = 0, cursorCol = 0;
        foreach (var (g, row, col) in pending.Where(p => p.row is null))
        {
            if (col is { } fixedCol)
            {
                if (cursorCol > fixedCol) cursorRow++;
                int r = cursorRow;
                while (!IsFree(r, fixedCol, g.RowSpan, g.ColSpan)) r++;
                g.Row = r;
                g.Col = fixedCol;
                cursorRow = r;
                cursorCol = fixedCol;
            }
            else
            {
                while (cursorCol + g.ColSpan > colCount || !IsFree(cursorRow, cursorCol, g.RowSpan, g.ColSpan))
                {
                    cursorCol++;
                    if (cursorCol + g.ColSpan > colCount) { cursorCol = 0; cursorRow++; }
                }
                g.Row = cursorRow;
                g.Col = cursorCol;
                cursorCol += g.ColSpan;
            }
            Occupy(g);
        }

        rowCount = Math.Max(explicitRows, placed.Count == 0 ? 0 : placed.Max(g => g.Row + g.RowSpan));
        colCount = Math.Max(colCount, placed.Count == 0 ? 0 : placed.Max(g => g.Col + g.ColSpan));
        return placed;
    }

    // ---- track sizing ----

    /// <summary>
    /// Sizes tracks in four steps: intrinsic base sizes from item contributions, growing toward
    /// limits, distributing leftover space to fr tracks, and optionally stretching auto tracks.
    /// A null <paramref name="available"/> means the container size is indefinite.
    /// </summary>
    static float[] SizeTracks(List<Track> tracks, float? available, float gap, List<Contribution> contributions, bool stretchAuto)
    {
        int n = tracks.Count;
        var size = new float[n];
        var limit = new float[n];
        var intrinsicMin = new float[n];
        var intrinsicMax = new float[n];

        float Fixed(TrackSize t) => t.Kind switch
        {
            TrackKind.Px => t.Value,
            TrackKind.Percent => available is { } a ? t.Value / 100f * a : 0,
            _ => 0
        };
        bool IsIntrinsic(TrackSize t) => t.Kind is TrackKind.Auto or TrackKind.MinContent or TrackKind.MaxContent;

        foreach (var c in contributions.Where(c => c.Span == 1 && c.Start < n))
        {
            intrinsicMin[c.Start] = Math.Max(intrinsicMin[c.Start], c.Min);
            intrinsicMax[c.Start] = Math.Max(intrinsicMax[c.Start], c.Max);
        }

        // Spanning items add whatever their span lacks, spread over its flexible-sized tracks.
        foreach (var c in contributions.Where(c => c.Span > 1 && c.Start < n).OrderBy(c => c.Span))
        {
            var span = Enumerable.Range(c.Start, Math.Min(c.Span, n - c.Start)).ToList();
            var targets = span.Where(i => IsIntrinsic(tracks[i].Min) || tracks[i].Max.Kind == TrackKind.Fr).ToList();
            if (targets.Count == 0) targets = span;

            float extraMin = c.Min - (span.Sum(i => intrinsicMin[i]) + gap * (span.Count - 1));
            if (extraMin > 0) foreach (int i in targets) intrinsicMin[i] += extraMin / targets.Count;

            float extraMax = c.Max - (span.Sum(i => intrinsicMax[i]) + gap * (span.Count - 1));
            if (extraMax > 0) foreach (int i in targets) intrinsicMax[i] += extraMax / targets.Count;
        }

        for (int i = 0; i < n; i++)
        {
            var t = tracks[i];
            size[i] = t.Min.Kind switch
            {
                TrackKind.Px or TrackKind.Percent => Fixed(t.Min),
                TrackKind.Auto or TrackKind.MinContent => intrinsicMin[i],
                TrackKind.MaxContent => intrinsicMax[i],
                _ => 0
            };
            limit[i] = t.Max.Kind switch
            {
                TrackKind.Px or TrackKind.Percent => Fixed(t.Max),
                TrackKind.MinContent => intrinsicMin[i],
                TrackKind.Auto or TrackKind.MaxContent => Math.Max(intrinsicMax[i], intrinsicMin[i]),
                _ => float.PositiveInfinity
            };
            if (limit[i] < size[i]) limit[i] = size[i];
        }

        float gaps = gap * Math.Max(0, n - 1);
        var frTracks = Enumerable.Range(0, n).Where(i => tracks[i].Max.Kind == TrackKind.Fr).ToList();

        if (available is { } avail)
        {
            // Grow tracks toward their limits, sharing the free space equally.
            float free = avail - size.Sum() - gaps;
            var growable = Enumerable.Range(0, n).Where(i => tracks[i].Max.Kind != TrackKind.Fr && size[i] < limit[i]).ToList();
            while (free > 0.01f && growable.Count > 0)
            {
                float share = free / growable.Count, used = 0;
                foreach (int i in growable)
                {
                    float add = Math.Min(share, limit[i] - size[i]);
                    size[i] += add;
                    used += add;
                }
                free -= used;
                growable.RemoveAll(i => size[i] >= limit[i] - 0.001f);
                if (used <= 0) break;
            }

            if (frTracks.Count > 0)
            {
                float leftover = avail - Enumerable.Range(0, n).Where(i => !frTracks.Contains(i)).Sum(i => size[i]) - gaps;
                var unfrozen = frTracks.ToList();
                float fraction = 0;
                while (unfrozen.Count > 0)
                {
                    float factorSum = unfrozen.Sum(i => tracks[i].Max.Value);
                    fraction = Math.Max(0, leftover) / Math.Max(factorSum, 1);
                    // A track whose share is below its content minimum is fixed at that minimum.
                    var violators = unfrozen.Where(i => fraction * tracks[i].Max.Value < size[i]).ToList();
                    if (violators.Count == 0) break;
                    foreach (int v in violators) { leftover -= size[v]; unfrozen.Remove(v); }
                }
                foreach (int i in unfrozen) size[i] = Math.Max(size[i], fraction * tracks[i].Max.Value);
            }
            else if (stretchAuto)
            {
                float spare = avail - size.Sum() - gaps;
                var autos = Enumerable.Range(0, n).Where(i => tracks[i].Max.Kind == TrackKind.Auto).ToList();
                if (spare > 0 && autos.Count > 0) foreach (int i in autos) size[i] += spare / autos.Count;
            }
        }
        else
        {
            // Indefinite container: auto tracks take their max-content size, fr tracks scale from it.
            for (int i = 0; i < n; i++)
                if (tracks[i].Max.Kind != TrackKind.Fr) size[i] = Math.Max(size[i], limit[i]);

            float fraction = 0;
            foreach (int i in frTracks)
                fraction = Math.Max(fraction, intrinsicMax[i] / Math.Max(tracks[i].Max.Value, 0.0001f));
            foreach (int i in frTracks)
                size[i] = Math.Max(size[i], fraction * tracks[i].Max.Value);
        }

        return size;
    }

    float GridContentWidth(ElementNode el, bool min)
    {
        var st = el.Style;
        var items = CollectItems(el, 0);
        var colTracks = st.GridColumns.Resolve(null, st.ColumnGap);
        var rowTracks = st.GridRows.Resolve(null, st.RowGap);
        var placed = PlaceGridItems(items, colTracks.Count, rowTracks.Count, out int colCount, out _);
        while (colTracks.Count < colCount) colTracks.Add(st.GridAutoColumns);
        if (colTracks.Count == 0) return 0;

        var contributions = placed.Select(g =>
        {
            float lo = OuterIntrinsicWidth(g.Item, min: true);
            return new Contribution(g.Col, g.ColSpan, lo, min ? lo : OuterIntrinsicWidth(g.Item, min: false));
        }).ToList();

        var sizes = SizeTracks(colTracks, null, st.ColumnGap, contributions, stretchAuto: false);
        return sizes.Sum() + st.ColumnGap * (sizes.Length - 1);
    }
}

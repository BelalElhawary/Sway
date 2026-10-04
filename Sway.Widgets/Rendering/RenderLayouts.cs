using SkiaSharp;

namespace Sway.Widgets;

public enum WrapAlignment { Start, End, Center, SpaceBetween, SpaceAround, SpaceEvenly }
public enum WrapCrossAlignment { Start, End, Center }

/// <summary>Flows children along an axis and starts a new run when they no longer fit.</summary>
public sealed class RenderWrap : RenderBoxContainer
{
    Axis _direction;
    WrapAlignment _alignment, _runAlignment;
    WrapCrossAlignment _crossAlignment;
    float _spacing, _runSpacing;
    TextDirection _textDirection;
    VerticalDirection _verticalDirection;

    public RenderWrap(Axis direction, WrapAlignment alignment, float spacing, WrapAlignment runAlignment, float runSpacing,
        WrapCrossAlignment crossAlignment, TextDirection textDirection, VerticalDirection verticalDirection)
    {
        _direction = direction; _alignment = alignment; _spacing = spacing; _runAlignment = runAlignment; _runSpacing = runSpacing;
        _crossAlignment = crossAlignment; _textDirection = textDirection; _verticalDirection = verticalDirection;
    }

    public void Update(Axis direction, WrapAlignment alignment, float spacing, WrapAlignment runAlignment, float runSpacing,
        WrapCrossAlignment crossAlignment, TextDirection textDirection, VerticalDirection verticalDirection)
    {
        _direction = direction; _alignment = alignment; _spacing = spacing; _runAlignment = runAlignment; _runSpacing = runSpacing;
        _crossAlignment = crossAlignment; _textDirection = textDirection; _verticalDirection = verticalDirection;
        MarkNeedsLayout();
    }

    bool Horizontal => _direction == Axis.Horizontal;
    float Main(Size s) => Horizontal ? s.Width : s.Height;
    float Cross(Size s) => Horizontal ? s.Height : s.Width;

    sealed class Run
    {
        public readonly List<RenderBox> Items = new();
        public float Main, Cross;
    }

    static (float leading, float between) Distribute(WrapAlignment a, float free, int count)
    {
        free = Math.Max(0, free);
        return a switch
        {
            WrapAlignment.End => (free, 0),
            WrapAlignment.Center => (free / 2, 0),
            WrapAlignment.SpaceBetween => (0, count > 1 ? free / (count - 1) : 0),
            WrapAlignment.SpaceAround => (count > 0 ? free / count / 2 : 0, count > 0 ? free / count : 0),
            WrapAlignment.SpaceEvenly => (free / (count + 1), free / (count + 1)),
            _ => (0, 0),
        };
    }

    protected override void PerformLayout()
    {
        float maxMain = Horizontal ? Constraints.MaxWidth : Constraints.MaxHeight;
        var childC = Horizontal ? new BoxConstraints(0, maxMain, 0, float.PositiveInfinity) : new BoxConstraints(0, float.PositiveInfinity, 0, maxMain);

        var runs = new List<Run>();
        var run = new Run();
        foreach (var c in Children)
        {
            c.Layout(childC);
            float m = Main(c.Size);
            float needed = run.Items.Count == 0 ? m : run.Main + _spacing + m;
            if (run.Items.Count > 0 && needed > maxMain)
            {
                runs.Add(run);
                run = new Run();
                needed = m;
            }
            run.Items.Add(c);
            run.Main = needed;
            run.Cross = Math.Max(run.Cross, Cross(c.Size));
        }
        if (run.Items.Count > 0) runs.Add(run);

        float contentMain = runs.Count == 0 ? 0 : runs.Max(r => r.Main);
        float contentCross = runs.Sum(r => r.Cross) + _runSpacing * Math.Max(0, runs.Count - 1);
        Size = Constraints.Constrain(Horizontal ? new Size(contentMain, contentCross) : new Size(contentCross, contentMain));

        float containerMain = Main(Size), containerCross = Cross(Size);
        bool flipMain = Horizontal ? _textDirection == TextDirection.Rtl : _verticalDirection == VerticalDirection.Up;
        bool flipCross = Horizontal ? _verticalDirection == VerticalDirection.Up : _textDirection == TextDirection.Rtl;

        var (runLead, runBetween) = Distribute(_runAlignment, containerCross - contentCross, runs.Count);
        float crossPos = runLead;
        foreach (var r in runs)
        {
            var (lead, between) = Distribute(_alignment, containerMain - r.Main, r.Items.Count);
            float mainPos = lead;
            foreach (var c in r.Items)
            {
                float cm = Main(c.Size), cc = Cross(c.Size);
                float off = _crossAlignment switch
                {
                    WrapCrossAlignment.End => r.Cross - cc,
                    WrapCrossAlignment.Center => (r.Cross - cc) / 2,
                    _ => 0,
                };
                float x = flipMain ? containerMain - mainPos - cm : mainPos;
                float y = flipCross ? containerCross - (crossPos + off) - cc : crossPos + off;
                SetOffset(c, Horizontal ? new Offset(x, y) : new Offset(y, x));
                mainPos += cm + _spacing + between;
            }
            crossPos += r.Cross + _runSpacing + runBetween;
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }

    public override float MaxIntrinsicWidth(float h) => Horizontal ? Children.Sum(c => c.MaxIntrinsicWidth(h)) + _spacing * Math.Max(0, Children.Count - 1) : Children.Select(c => c.MaxIntrinsicWidth(h)).DefaultIfEmpty(0).Max();
    public override float MinIntrinsicWidth(float h) => Children.Select(c => c.MinIntrinsicWidth(h)).DefaultIfEmpty(0).Max();
}

public enum TrackKind { Px, Fr, Auto }

/// <summary>A grid track size: fixed pixels, a flexible share (fr) of the remaining space, or sized by its content.</summary>
public readonly record struct GridTrack(TrackKind Kind, float Value = 0)
{
    public static GridTrack Px(float v) => new(TrackKind.Px, v);
    public static GridTrack Fr(float v = 1) => new(TrackKind.Fr, v);
    public static readonly GridTrack Auto = new(TrackKind.Auto);
    public static implicit operator GridTrack(float px) => Px(px);
}

public sealed class GridParentData : BoxParentData
{
    public int? Column, Row;
    public int ColumnSpan = 1, RowSpan = 1;
    public Alignment? Alignment;
    internal int R, C;
}

/// <summary>
/// CSS-grid-style layout: explicit column and row tracks (px, fr, auto), gaps, item spans, explicit or automatic
/// (row-major, sparse) placement, and per-item alignment.
/// </summary>
public sealed class RenderGrid : RenderBoxContainer
{
    IReadOnlyList<GridTrack> _columns, _rows;
    GridTrack _autoRow;
    float _columnGap, _rowGap, _autoFillMin;

    public RenderGrid(IReadOnlyList<GridTrack> columns, IReadOnlyList<GridTrack> rows, GridTrack autoRow, float columnGap, float rowGap, float autoFillMin)
    {
        _columns = columns; _rows = rows; _autoRow = autoRow; _columnGap = columnGap; _rowGap = rowGap; _autoFillMin = autoFillMin;
    }

    public void Update(IReadOnlyList<GridTrack> columns, IReadOnlyList<GridTrack> rows, GridTrack autoRow, float columnGap, float rowGap, float autoFillMin)
    {
        _columns = columns; _rows = rows; _autoRow = autoRow; _columnGap = columnGap; _rowGap = rowGap; _autoFillMin = autoFillMin;
        MarkNeedsLayout();
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not GridParentData) child.ParentData = new GridParentData();
    }

    static GridParentData PD(RenderBox c) => (GridParentData)c.ParentData!;

    protected override void PerformLayout()
    {
        float maxW = Constraints.MaxWidth, maxH = Constraints.MaxHeight;

        // --- tracks: columns may be generated by auto-fill ---
        var cols = _columns.ToList();
        if (_autoFillMin > 0 && float.IsFinite(maxW))
        {
            int n = Math.Max(1, (int)MathF.Floor((maxW + _columnGap) / (_autoFillMin + _columnGap)));
            cols = Enumerable.Repeat(GridTrack.Fr(1), n).ToList();
        }
        if (cols.Count == 0) cols.Add(GridTrack.Fr(1));

        // --- placement ---
        var occupied = new List<bool[]>();
        bool[] RowOcc(int r) { while (occupied.Count <= r) occupied.Add(new bool[cols.Count]); return occupied[r]; }
        bool Fits(int r, int c, int rs, int cs)
        {
            if (c + cs > cols.Count) return false;
            for (int i = r; i < r + rs; i++) for (int j = c; j < c + cs; j++) if (RowOcc(i)[j]) return false;
            return true;
        }
        void Mark(int r, int c, int rs, int cs) { for (int i = r; i < r + rs; i++) for (int j = c; j < c + cs; j++) RowOcc(i)[j] = true; }

        foreach (var child in Children)
        {
            var pd = PD(child);
            pd.ColumnSpan = Math.Clamp(pd.ColumnSpan, 1, cols.Count);
            pd.RowSpan = Math.Max(1, pd.RowSpan);
            if (pd.Column is { } col && pd.Row is { } row)
            {
                pd.C = Math.Clamp(col, 0, cols.Count - pd.ColumnSpan); pd.R = Math.Max(0, row);
                Mark(pd.R, pd.C, pd.RowSpan, pd.ColumnSpan);
                pd.Offset = pd.Offset; // placed
            }
            else pd.R = -1;
        }

        int curR = 0, curC = 0;
        foreach (var child in Children)
        {
            var pd = PD(child);
            if (pd.R >= 0) continue;
            if (pd.Column is { } fixedCol)
            {
                int c = Math.Clamp(fixedCol, 0, cols.Count - pd.ColumnSpan), r = 0;
                while (!Fits(r, c, pd.RowSpan, pd.ColumnSpan)) r++;
                (pd.R, pd.C) = (r, c);
                Mark(r, c, pd.RowSpan, pd.ColumnSpan);
                continue;
            }
            while (true)
            {
                if (curC + pd.ColumnSpan > cols.Count) { curC = 0; curR++; }
                if (Fits(curR, curC, pd.RowSpan, pd.ColumnSpan)) break;
                curC++;
            }
            (pd.R, pd.C) = (curR, curC);
            Mark(curR, curC, pd.RowSpan, pd.ColumnSpan);
            curC += pd.ColumnSpan;
        }

        int rowCount = Math.Max(occupied.Count, Children.Count == 0 ? 0 : Children.Max(ch => PD(ch).R + PD(ch).RowSpan));
        rowCount = Math.Max(rowCount, _rows.Count);
        var rows = new List<GridTrack>(_rows);
        while (rows.Count < rowCount) rows.Add(_autoRow);

        // --- column sizes ---
        var colW = new float[cols.Count];
        float colGaps = _columnGap * (cols.Count - 1);
        for (int i = 0; i < cols.Count; i++)
            if (cols[i].Kind == TrackKind.Px) colW[i] = cols[i].Value;

        void GrowAuto(float[] sizes, IReadOnlyList<GridTrack> tracks, int start, int span, float need, float gap)
        {
            float have = gap * (span - 1);
            for (int k = start; k < start + span; k++) have += sizes[k];
            if (need <= have) return;
            var autos = Enumerable.Range(start, span).Where(k => tracks[k].Kind != TrackKind.Px).ToList();
            if (autos.Count == 0) return;
            float add = (need - have) / autos.Count;
            foreach (var k in autos) sizes[k] += add;
        }

        // Single-column items first, then spanning ones.
        foreach (var span1 in new[] { true, false })
            foreach (var child in Children)
            {
                var pd = PD(child);
                if ((pd.ColumnSpan == 1) != span1) continue;
                bool flexOnly = Enumerable.Range(pd.C, pd.ColumnSpan).All(k => cols[k].Kind == TrackKind.Fr) && float.IsFinite(maxW);
                if (flexOnly) continue; // fr tracks take what is left, not what content wants
                if (Enumerable.Range(pd.C, pd.ColumnSpan).All(k => cols[k].Kind == TrackKind.Px)) continue;
                float need = child.MaxIntrinsicWidth(float.PositiveInfinity);
                GrowAuto(colW, cols, pd.C, pd.ColumnSpan, need, _columnGap);
            }

        float frTotal = cols.Where(c => c.Kind == TrackKind.Fr).Sum(c => c.Value);
        bool hasFr = frTotal > 0;
        if (hasFr && float.IsFinite(maxW))
        {
            float used = colGaps;
            for (int i = 0; i < cols.Count; i++) if (cols[i].Kind != TrackKind.Fr) used += colW[i];
            float free = Math.Max(0, maxW - used);
            for (int i = 0; i < cols.Count; i++)
                if (cols[i].Kind == TrackKind.Fr) colW[i] = free * cols[i].Value / frTotal;
        }

        float ColSpanWidth(GridParentData pd)
        {
            float w = _columnGap * (pd.ColumnSpan - 1);
            for (int k = pd.C; k < pd.C + pd.ColumnSpan; k++) w += colW[k];
            return w;
        }

        // --- row sizes ---
        var rowH = new float[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Kind == TrackKind.Px) rowH[i] = rows[i].Value;

        float rowGaps = _rowGap * Math.Max(0, rows.Count - 1);
        bool rowsFlex = rows.Any(r => r.Kind == TrackKind.Fr) && float.IsFinite(maxH);
        foreach (var span1 in new[] { true, false })
            foreach (var child in Children)
            {
                var pd = PD(child);
                if ((pd.RowSpan == 1) != span1) continue;
                if (Enumerable.Range(pd.R, pd.RowSpan).All(k => rows[k].Kind == TrackKind.Px)) continue;
                if (rowsFlex && Enumerable.Range(pd.R, pd.RowSpan).All(k => rows[k].Kind == TrackKind.Fr)) continue;
                float w = ColSpanWidth(pd);
                child.Layout(new BoxConstraints(w, w, 0, float.PositiveInfinity));
                GrowAuto(rowH, rows, pd.R, pd.RowSpan, child.Size.Height, _rowGap);
            }

        float rowFr = rows.Where(r => r.Kind == TrackKind.Fr).Sum(r => r.Value);
        if (rowFr > 0 && float.IsFinite(maxH))
        {
            float used = rowGaps;
            for (int i = 0; i < rows.Count; i++) if (rows[i].Kind != TrackKind.Fr) used += rowH[i];
            float free = Math.Max(0, maxH - used);
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Kind == TrackKind.Fr) rowH[i] = free * rows[i].Value / rowFr;
        }

        float totalW = colW.Sum() + colGaps, totalH = rowH.Sum() + rowGaps;
        Size = Constraints.Constrain(new Size(totalW, totalH));

        // --- final placement ---
        var colX = new float[cols.Count];
        float x = 0;
        for (int i = 0; i < cols.Count; i++) { colX[i] = x; x += colW[i] + _columnGap; }
        var rowY = new float[rows.Count];
        float y = 0;
        for (int i = 0; i < rows.Count; i++) { rowY[i] = y; y += rowH[i] + _rowGap; }

        foreach (var child in Children)
        {
            var pd = PD(child);
            float cw = ColSpanWidth(pd);
            float ch = _rowGap * (pd.RowSpan - 1);
            for (int k = pd.R; k < pd.R + pd.RowSpan; k++) ch += rowH[k];

            if (pd.Alignment is { } align)
            {
                child.Layout(new BoxConstraints(0, cw, 0, ch));
                pd.Offset = new Offset(colX[pd.C], rowY[pd.R]) + align.AlongSize(new Size(cw, ch), child.Size);
            }
            else
            {
                child.Layout(BoxConstraints.Tight(new Size(cw, ch)));
                pd.Offset = new Offset(colX[pd.C], rowY[pd.R]);
            }
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }
}

/// <summary>Applies a blur and/or colour filter to everything painted by its child.</summary>
public sealed class RenderImageFilter(float blur, SKColorFilter? colorFilter) : RenderProxyBox
{
    float _blur = blur;
    SKColorFilter? _colorFilter = colorFilter;

    public void Update(float blur, SKColorFilter? colorFilter)
    {
        if (_blur == blur && ReferenceEquals(_colorFilter, colorFilter)) return;
        _blur = blur; _colorFilter = colorFilter;
        MarkNeedsPaint();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } c) return;
        if (_blur <= 0 && _colorFilter is null) { context.PaintChild(c, offset); return; }
        using var paint = new SKPaint { ColorFilter = _colorFilter };
        if (_blur > 0) paint.ImageFilter = SKImageFilter.CreateBlur(_blur, _blur);
        context.Canvas.SaveLayer(paint);
        context.PaintChild(c, offset);
        context.Canvas.Restore();
    }
}

/// <summary>Filters whatever has already been painted behind it (within its own bounds), then paints its child on top.</summary>
public sealed class RenderBackdropFilter(float blur, SKColorFilter? colorFilter) : RenderProxyBox
{
    float _blur = blur;
    SKColorFilter? _colorFilter = colorFilter;

    public void Update(float blur, SKColorFilter? colorFilter)
    {
        if (_blur == blur && ReferenceEquals(_colorFilter, colorFilter)) return;
        _blur = blur; _colorFilter = colorFilter;
        MarkNeedsPaint();
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (_blur <= 0 && _colorFilter is null)
        {
            if (Child is { } plain) context.PaintChild(plain, offset);
            return;
        }

        var canvas = context.Canvas;
        canvas.Save();
        canvas.ClipRect(Size.ToRect(offset).ToSk(), antialias: true);

        // Clamp keeps the blur from pulling in transparent black at the clip's edges.
        SKImageFilter? filter = _blur > 0 ? SKImageFilter.CreateBlur(_blur, _blur, SKShaderTileMode.Clamp) : null;
        if (_colorFilter is not null) filter = SKImageFilter.CreateColorFilter(_colorFilter, filter);

        canvas.SaveLayer(new SKCanvasSaveLayerRec { Backdrop = filter });
        if (Child is { } c) context.PaintChild(c, offset);
        canvas.Restore();
        canvas.Restore();
        filter?.Dispose();
    }
}

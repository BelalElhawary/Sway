namespace Sway.Widgets;

/// <summary>Row/Column layout: lays out inflexible children first, then shares the remaining space among flexible ones.</summary>
public sealed class RenderFlex : RenderBoxContainer
{
    Axis _direction;
    MainAxisAlignment _mainAxisAlignment;
    MainAxisSize _mainAxisSize;
    CrossAxisAlignment _crossAxisAlignment;
    TextDirection _textDirection;
    VerticalDirection _verticalDirection;
    float _spacing;

    public RenderFlex(Axis direction, MainAxisAlignment mainAxisAlignment, MainAxisSize mainAxisSize,
        CrossAxisAlignment crossAxisAlignment, TextDirection textDirection, VerticalDirection verticalDirection, float spacing)
    {
        _direction = direction; _mainAxisAlignment = mainAxisAlignment; _mainAxisSize = mainAxisSize;
        _crossAxisAlignment = crossAxisAlignment; _textDirection = textDirection;
        _verticalDirection = verticalDirection; _spacing = spacing;
    }

    public void Update(Axis direction, MainAxisAlignment mainAxisAlignment, MainAxisSize mainAxisSize,
        CrossAxisAlignment crossAxisAlignment, TextDirection textDirection, VerticalDirection verticalDirection, float spacing)
    {
        if (_direction == direction && _mainAxisAlignment == mainAxisAlignment && _mainAxisSize == mainAxisSize
            && _crossAxisAlignment == crossAxisAlignment && _textDirection == textDirection
            && _verticalDirection == verticalDirection && _spacing == spacing) return;
        _direction = direction; _mainAxisAlignment = mainAxisAlignment; _mainAxisSize = mainAxisSize;
        _crossAxisAlignment = crossAxisAlignment; _textDirection = textDirection;
        _verticalDirection = verticalDirection; _spacing = spacing;
        MarkNeedsLayout();
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not FlexParentData) child.ParentData = new FlexParentData();
    }

    bool Horizontal => _direction == Axis.Horizontal;
    float Main(Size s) => Horizontal ? s.Width : s.Height;
    float Cross(Size s) => Horizontal ? s.Height : s.Width;
    static int FlexOf(RenderBox c) => ((FlexParentData)c.ParentData!).Flex;
    static FlexFit FitOf(RenderBox c) => ((FlexParentData)c.ParentData!).Fit;

    // Flip the main axis for RTL rows and upward columns.
    bool FlipMainAxis => Horizontal ? _textDirection == TextDirection.Rtl : _verticalDirection == VerticalDirection.Up;
    // Flip the cross axis for RTL columns and upward rows.
    bool FlipCrossAxis => Horizontal ? _verticalDirection == VerticalDirection.Up : _textDirection == TextDirection.Rtl;

    BoxConstraints ChildConstraints(float minMain, float maxMain, float maxCross)
    {
        bool stretch = _crossAxisAlignment == CrossAxisAlignment.Stretch;
        float minCross = stretch && float.IsFinite(maxCross) ? maxCross : 0;
        return Horizontal
            ? new BoxConstraints(minMain, maxMain, minCross, maxCross)
            : new BoxConstraints(minCross, maxCross, minMain, maxMain);
    }

    protected override void PerformLayout()
    {
        var children = Children;
        float maxMain = Horizontal ? Constraints.MaxWidth : Constraints.MaxHeight;
        float maxCross = Horizontal ? Constraints.MaxHeight : Constraints.MaxWidth;
        bool canFlex = float.IsFinite(maxMain);

        float allocated = _spacing * Math.Max(0, children.Count - 1);
        float crossSize = 0;
        int totalFlex = 0;
        RenderBox? lastFlex = null;

        float maxAscent = 0, maxDescent = 0;
        bool baseline = _crossAxisAlignment == CrossAxisAlignment.Baseline && Horizontal;

        void TrackBaseline(RenderBox c)
        {
            if (!baseline) return;
            if (c.GetDistanceToBaseline() is { } b)
            {
                maxAscent = Math.Max(maxAscent, b);
                maxDescent = Math.Max(maxDescent, c.Size.Height - b);
            }
        }

        foreach (var c in children)
        {
            int flex = FlexOf(c);
            if (flex > 0)
            {
                totalFlex += flex;
                lastFlex = c;
                continue;
            }
            c.Layout(ChildConstraints(0, float.PositiveInfinity, maxCross));
            allocated += Main(c.Size);
            crossSize = Math.Max(crossSize, Cross(c.Size));
            TrackBaseline(c);
        }

        if (totalFlex > 0)
        {
            float free = Math.Max(0, (canFlex ? maxMain : 0) - allocated);
            float perFlex = canFlex ? free / totalFlex : 0;
            float flexUsed = 0;
            foreach (var c in children)
            {
                int flex = FlexOf(c);
                if (flex <= 0) continue;
                float maxExtent = canFlex ? (ReferenceEquals(c, lastFlex) ? free - flexUsed : perFlex * flex) : float.PositiveInfinity;
                float minExtent = FitOf(c) == FlexFit.Tight && canFlex ? maxExtent : 0;
                c.Layout(ChildConstraints(minExtent, maxExtent, maxCross));
                allocated += Main(c.Size);
                flexUsed += maxExtent;
                crossSize = Math.Max(crossSize, Cross(c.Size));
                TrackBaseline(c);
            }
        }

        if (baseline) crossSize = Math.Max(crossSize, maxAscent + maxDescent);

        float ideal = canFlex && _mainAxisSize == MainAxisSize.Max ? maxMain : allocated;
        Size = Constraints.Constrain(Horizontal ? new Size(ideal, crossSize) : new Size(crossSize, ideal));
        float actualMain = Main(Size);
        crossSize = Cross(Size);

        OverflowExtent = Math.Max(0, allocated - actualMain);
        float remaining = Math.Max(0, actualMain - allocated);
        int count = children.Count;
        float leading = 0, between = _spacing;
        switch (_mainAxisAlignment)
        {
            case MainAxisAlignment.End: leading = remaining; break;
            case MainAxisAlignment.Center: leading = remaining / 2; break;
            case MainAxisAlignment.SpaceBetween: between += count > 1 ? remaining / (count - 1) : 0; break;
            case MainAxisAlignment.SpaceAround: { float s = count > 0 ? remaining / count : 0; leading = s / 2; between += s; break; }
            case MainAxisAlignment.SpaceEvenly: { float s = count > 0 ? remaining / (count + 1) : 0; leading = s; between += s; break; }
        }

        bool flipMain = FlipMainAxis, flipCross = FlipCrossAxis;
        float pos = leading;
        foreach (var c in children)
        {
            float childMain = Main(c.Size), childCross = Cross(c.Size);
            float crossPos = _crossAxisAlignment switch
            {
                CrossAxisAlignment.Start => flipCross ? crossSize - childCross : 0,
                CrossAxisAlignment.End => flipCross ? 0 : crossSize - childCross,
                CrossAxisAlignment.Center => (crossSize - childCross) / 2,
                CrossAxisAlignment.Baseline when baseline && c.GetDistanceToBaseline() is { } b => maxAscent - b,
                _ => flipCross ? crossSize - childCross : 0,
            };

            float mainPos = flipMain ? actualMain - pos - childMain : pos;
            SetOffset(c, Horizontal ? new Offset(mainPos, crossPos) : new Offset(crossPos, mainPos));
            pos += childMain + between;
        }
    }

    /// <summary>How far the children extend past this box along the main axis, in pixels (0 when they fit).</summary>
    public float OverflowExtent { get; private set; }

    /// <summary>Whether overflowing flexes paint a hazard stripe at their far edge. On in debug builds, like Flutter.</summary>
    public static bool PaintOverflowIndicators { get; set; } =
#if DEBUG
        true;
#else
        false;
#endif

    public override void Paint(PaintingContext context, Offset offset)
    {
        foreach (var c in Children) context.PaintChild(c, offset + OffsetOf(c));
        if (PaintOverflowIndicators && OverflowExtent > 0.5f) PaintOverflow(context.Canvas, offset);
    }

    // A yellow and black diagonal band along the edge the content spills past, so the problem is visible during development.
    void PaintOverflow(SkiaSharp.SKCanvas canvas, Offset offset)
    {
        const float band = 8;
        bool atStart = FlipMainAxis;
        var r = Horizontal
            ? new SkiaSharp.SKRect(offset.Dx + (atStart ? 0 : Size.Width - band), offset.Dy, offset.Dx + (atStart ? band : Size.Width), offset.Dy + Size.Height)
            : new SkiaSharp.SKRect(offset.Dx, offset.Dy + (atStart ? 0 : Size.Height - band), offset.Dx + Size.Width, offset.Dy + (atStart ? band : Size.Height));
        canvas.Save();
        canvas.ClipRect(r);
        using var yellow = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0xFF, 0xD6, 0x00) };
        canvas.DrawRect(r, yellow);
        using var black = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, StrokeWidth = 4, IsAntialias = true };
        // Stripes run along the band's long side, so a vertical band is striped over its whole height too.
        float reach = Math.Max(r.Width, r.Height) + band * 2;
        for (float d = -band * 2; d < reach; d += band)
        {
            if (Horizontal) canvas.DrawLine(r.Left, r.Top + d, r.Left + band, r.Top + d + band, black);
            else canvas.DrawLine(r.Left + d, r.Top, r.Left + d + band, r.Top + band, black);
        }
        canvas.Restore();
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }

    public override float? GetDistanceToBaseline()
    {
        if (!Horizontal) return Children.Count > 0 ? Children[0].GetDistanceToBaseline() + OffsetOf(Children[0]).Dy : null;
        float? best = null;
        foreach (var c in Children)
            if (c.GetDistanceToBaseline() is { } b) best = Math.Min(best ?? float.MaxValue, b + OffsetOf(c).Dy);
        return best;
    }

    // Intrinsics: sum on the main axis, max on the cross axis (flex children measured by their natural size).
    float MainIntrinsic(Func<RenderBox, float> f) => Children.Sum(f) + _spacing * Math.Max(0, Children.Count - 1);
    float CrossIntrinsic(Func<RenderBox, float> f) => Children.Count == 0 ? 0 : Children.Max(f);

    public override float MinIntrinsicWidth(float h) => Horizontal ? MainIntrinsic(c => c.MinIntrinsicWidth(h)) : CrossIntrinsic(c => c.MinIntrinsicWidth(h));
    public override float MaxIntrinsicWidth(float h) => Horizontal ? MainIntrinsic(c => c.MaxIntrinsicWidth(h)) : CrossIntrinsic(c => c.MaxIntrinsicWidth(h));
    public override float MinIntrinsicHeight(float w) => Horizontal ? CrossIntrinsic(c => c.MinIntrinsicHeight(w)) : MainIntrinsic(c => c.MinIntrinsicHeight(w));
    public override float MaxIntrinsicHeight(float w) => Horizontal ? CrossIntrinsic(c => c.MaxIntrinsicHeight(w)) : MainIntrinsic(c => c.MaxIntrinsicHeight(w));
}

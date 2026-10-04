namespace Sway.Widgets;

public enum MainAxisAlignment { Start, End, Center, SpaceBetween, SpaceAround, SpaceEvenly }
public enum MainAxisSize { Min, Max }
public enum CrossAxisAlignment { Start, End, Center, Stretch, Baseline }
public enum VerticalDirection { Up, Down }
public enum FlexFit { Tight, Loose }

public sealed class FlexParentData : BoxParentData
{
    public int Flex;
    public FlexFit Fit = FlexFit.Tight;
}

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

public enum StackFit { Loose, Expand, Passthrough }

public sealed class StackParentData : BoxParentData
{
    public float? Left, Top, Right, Bottom, Width, Height;
    public bool IsPositioned => Left is not null || Top is not null || Right is not null || Bottom is not null || Width is not null || Height is not null;
}

/// <summary>Overlaps children; positioned ones are placed by their edges, the rest by <c>alignment</c>.</summary>
public sealed class RenderStack(Alignment alignment, StackFit fit, bool clip) : RenderBoxContainer
{
    Alignment _alignment = alignment;
    StackFit _fit = fit;
    bool _clip = clip;

    public void Update(Alignment alignment, StackFit fit, bool clip)
    {
        if (_alignment == alignment && _fit == fit && _clip == clip) return;
        _alignment = alignment; _fit = fit; _clip = clip;
        MarkNeedsLayout();
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not StackParentData) child.ParentData = new StackParentData();
    }

    protected override void PerformLayout()
    {
        bool hasNonPositioned = false;
        float width = Constraints.MinWidth, height = Constraints.MinHeight;
        var nonPositioned = _fit switch
        {
            StackFit.Loose => Constraints.Loosen(),
            StackFit.Expand => BoxConstraints.Tight(Constraints.Biggest),
            _ => Constraints,
        };

        foreach (var c in Children)
        {
            var pd = (StackParentData)c.ParentData!;
            if (pd.IsPositioned) continue;
            hasNonPositioned = true;
            c.Layout(nonPositioned);
            width = Math.Max(width, c.Size.Width);
            height = Math.Max(height, c.Size.Height);
        }

        Size = hasNonPositioned ? Constraints.Constrain(new Size(width, height)) : Constraints.Biggest;

        foreach (var c in Children)
        {
            var pd = (StackParentData)c.ParentData!;
            if (!pd.IsPositioned)
            {
                pd.Offset = _alignment.AlongSize(Size, c.Size);
                continue;
            }

            var cc = new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity);
            if (pd.Left is { } l && pd.Right is { } r) cc = cc.Tighten(width: Math.Max(0, Size.Width - l - r));
            else if (pd.Width is { } w) cc = cc.Tighten(width: w);
            if (pd.Top is { } t && pd.Bottom is { } b) cc = cc.Tighten(height: Math.Max(0, Size.Height - t - b));
            else if (pd.Height is { } h) cc = cc.Tighten(height: h);
            c.Layout(cc);

            float x = pd.Left ?? (pd.Right is { } rr ? Size.Width - rr - c.Size.Width : _alignment.AlongSize(Size, c.Size).Dx);
            float y = pd.Top ?? (pd.Bottom is { } bb ? Size.Height - bb - c.Size.Height : _alignment.AlongSize(Size, c.Size).Dy);
            pd.Offset = new Offset(x, y);
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        void PaintAll(PaintingContext ctx, Offset o)
        {
            foreach (var c in Children) ctx.PaintChild(c, o + OffsetOf(c));
        }
        if (_clip) context.PushClipRect(offset, Size.ToRect(), PaintAll);
        else PaintAll(context, offset);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }

    public override float MinIntrinsicWidth(float h) => Children.Count == 0 ? 0 : Children.Max(c => c.MinIntrinsicWidth(h));
    public override float MaxIntrinsicWidth(float h) => Children.Count == 0 ? 0 : Children.Max(c => c.MaxIntrinsicWidth(h));
    public override float MinIntrinsicHeight(float w) => Children.Count == 0 ? 0 : Children.Max(c => c.MinIntrinsicHeight(w));
    public override float MaxIntrinsicHeight(float w) => Children.Count == 0 ? 0 : Children.Max(c => c.MaxIntrinsicHeight(w));
}

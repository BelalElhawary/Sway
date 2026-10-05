using SkiaSharp;

namespace Sway.Widgets;

/// <summary>
/// Viewport that creates only the visible items, building them during layout. Items share one extent when
/// <c>itemExtent</c> is given; otherwise each is measured when first shown, unmeasured ones are estimated from the
/// average, and the scroll offset is corrected as measurements arrive so visible content does not jump.
/// </summary>
public sealed class RenderLazyViewport : RenderBoxContainer, IScrollViewport
{
    const float InitialEstimate = 50;

    Axis _axis;
    ScrollPosition _position = new();
    int _itemCount;
    float? _itemExtent;
    EdgeInsets _padding;
    readonly ScrollbarInteraction _scrollbar = new();

    // Variable-extent bookkeeping: NaN marks an item that has not been measured yet.
    float[] _extents = Array.Empty<float>();
    float _knownSum;
    int _knownCount;
    float _extentsCross = -1;
    int _anchorIndex;
    float _anchorOffset;

    public Action<int, int>? BuildRange;

    public void Configure(Axis axis, ScrollPosition position, int itemCount, float? itemExtent, EdgeInsets padding)
    {
        if (!ReferenceEquals(_position, position))
        {
            if (Owner is not null) _position.Changed -= OnScroll;
            _position = position;
            if (Owner is not null) _position.Changed += OnScroll;
        }
        if (_axis != axis) ResetExtents();
        _axis = axis; _itemCount = itemCount; _itemExtent = itemExtent; _padding = padding;
        MarkNeedsLayout();
    }

    void OnScroll() => MarkNeedsLayout();

    Axis IScrollViewport.ScrollAxis => _axis;
    ScrollPosition IScrollViewport.Position => _position;
    Offset IScrollViewport.PaintShift => Offset.Zero;

    public override void Attach(PipelineOwner owner)
    {
        base.Attach(owner);
        _position.Changed += OnScroll;
    }

    public override void Detach()
    {
        _position.Changed -= OnScroll;
        base.Detach();
    }

    protected override void SetupParentData(RenderObject child)
    {
        if (child.ParentData is not LazyListParentData) child.ParentData = new LazyListParentData();
    }

    bool Vertical => _axis == Axis.Vertical;

    void ResetExtents()
    {
        _extents = Array.Empty<float>();
        _knownSum = 0;
        _knownCount = 0;
        _anchorIndex = 0;
        _anchorOffset = 0;
    }

    void EnsureExtents(float cross)
    {
        // Item sizes depend on the width they wrap to, so a different cross size invalidates every measurement.
        if (_extentsCross != cross)
        {
            ResetExtents();
            _extentsCross = cross;
        }
        if (_extents.Length == _itemCount) return;

        var resized = new float[_itemCount];
        Array.Fill(resized, float.NaN);
        int keep = Math.Min(_extents.Length, _itemCount);
        Array.Copy(_extents, resized, keep);
        _extents = resized;
        _knownSum = 0;
        _knownCount = 0;
        for (int i = 0; i < keep; i++)
            if (!float.IsNaN(resized[i])) { _knownSum += resized[i]; _knownCount++; }
    }

    float Estimate => _knownCount > 0 ? _knownSum / _knownCount : InitialEstimate;

    float ExtentOf(int i) => float.IsNaN(_extents[i]) ? Estimate : _extents[i];

    void Record(int i, float extent)
    {
        if (!float.IsNaN(_extents[i])) { _knownSum -= _extents[i]; _knownCount--; }
        _extents[i] = extent;
        _knownSum += extent;
        _knownCount++;
    }

    /// <summary>Content offset of the start of item <paramref name="index"/>, counting padding and estimating unmeasured items.</summary>
    float PrefixOffset(int index, float padStart)
    {
        float estimate = Estimate, offset = padStart;
        for (int i = 0; i < index; i++) offset += float.IsNaN(_extents[i]) ? estimate : _extents[i];
        return offset;
    }

    protected override void PerformLayout()
    {
        var c = Constraints;
        float viewport = Vertical ? (c.HasBoundedHeight ? c.MaxHeight : 600) : (c.HasBoundedWidth ? c.MaxWidth : 600);
        float cross = Vertical ? (c.HasBoundedWidth ? c.MaxWidth : 300) : (c.HasBoundedHeight ? c.MaxHeight : 300);
        Size = c.Constrain(Vertical ? new Size(cross, viewport) : new Size(viewport, cross));
        viewport = Vertical ? Size.Height : Size.Width;
        cross = Vertical ? Size.Width : Size.Height;

        float padStart = Vertical ? _padding.Top : _padding.Left;
        float padEnd = Vertical ? _padding.Bottom : _padding.Right;
        float crossPad = Vertical ? _padding.Horizontal : _padding.Vertical;
        float crossStart = Vertical ? _padding.Left : _padding.Top;
        float innerCross = Math.Max(0, cross - crossPad);

        if (_itemCount == 0)
        {
            BuildRange?.Invoke(0, -1);
            _position.ApplyContentDimensions(viewport, 0);
            return;
        }

        if (_itemExtent is { } fixedExtent && fixedExtent > 0)
            LayoutFixed(fixedExtent, viewport, padStart, padEnd, crossStart, innerCross);
        else
            LayoutVariable(viewport, padStart, padEnd, crossStart, innerCross);
    }

    BoxConstraints ExactExtent(float extent, float innerCross) => Vertical
        ? new BoxConstraints(innerCross, innerCross, extent, extent)
        : new BoxConstraints(extent, extent, innerCross, innerCross);

    BoxConstraints FreeExtent(float innerCross) => Vertical
        ? new BoxConstraints(innerCross, innerCross, 0, float.PositiveInfinity)
        : new BoxConstraints(0, float.PositiveInfinity, innerCross, innerCross);

    Offset Place(float main, float crossStart) => Vertical ? new Offset(crossStart, main) : new Offset(main, crossStart);

    void LayoutFixed(float extent, float viewport, float padStart, float padEnd, float crossStart, float innerCross)
    {
        float total = padStart + extent * _itemCount + padEnd;
        _position.ApplyContentDimensions(viewport, total - viewport);

        float scroll = _position.Pixels;
        int first = Math.Max(0, (int)MathF.Floor((scroll - padStart) / extent));
        int last = Math.Min(_itemCount - 1, (int)MathF.Ceiling((scroll + viewport - padStart) / extent) - 1);
        BuildRange?.Invoke(first, last);

        foreach (var child in Children)
        {
            var pd = (LazyListParentData)child.ParentData!;
            child.Layout(ExactExtent(extent, innerCross));
            pd.Offset = Place(padStart + pd.Index * extent - scroll, crossStart);
        }
    }

    void LayoutVariable(float viewport, float padStart, float padEnd, float crossStart, float innerCross)
    {
        EnsureExtents(innerCross);
        if (_anchorIndex >= _itemCount) { _anchorIndex = 0; _anchorOffset = padStart; }

        float scroll = _position.Pixels;
        int first = 0;

        // A pass picks the visible range from the current estimates and measures it. Measuring changes the estimates, so
        // the anchor (the first item of the previous layout) is re-located and the scroll offset shifted to keep it still;
        // a second pass then lays out the range around the corrected offset.
        for (int pass = 0; pass < 4; pass++)
        {
            first = 0;
            float firstOffset = padStart;
            while (first < _itemCount - 1 && firstOffset + ExtentOf(first) <= scroll)
            {
                firstOffset += ExtentOf(first);
                first++;
            }

            int last = Math.Min(_itemCount - 1, first + Math.Max(8, (int)MathF.Ceiling(viewport / Estimate) + 1));
            while (true)
            {
                BuildRange?.Invoke(first, last);
                foreach (var child in Children)
                {
                    int index = ((LazyListParentData)child.ParentData!).Index;
                    child.Layout(FreeExtent(innerCross));
                    Record(index, Math.Max(0, Vertical ? child.Size.Height : child.Size.Width));
                }
                float cursor = firstOffset;
                for (int i = first; i <= last; i++) cursor += ExtentOf(i);
                if (cursor >= scroll + viewport || last >= _itemCount - 1) break;
                last = Math.Min(_itemCount - 1, last + Math.Max(8, last - first));
            }

            float anchorNow = PrefixOffset(_anchorIndex, padStart);
            float delta = anchorNow - _anchorOffset;
            if (MathF.Abs(delta) < 0.01f) break;
            _anchorOffset = anchorNow;
            scroll = Math.Max(0, scroll + delta);
        }

        float startOffset = PrefixOffset(first, padStart);
        float total = padStart + _knownSum + (_itemCount - _knownCount) * Estimate + padEnd;
        _position.ApplyContentDimensions(viewport, total - viewport);
        _position.CorrectTo(scroll);
        scroll = _position.Pixels;

        foreach (var child in Children)
        {
            var pd = (LazyListParentData)child.ParentData!;
            float itemMain = startOffset;
            for (int i = first; i < pd.Index; i++) itemMain += ExtentOf(i);
            pd.Offset = Place(itemMain - scroll, crossStart);
        }

        _anchorIndex = first;
        _anchorOffset = startOffset;
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        context.PushClipRect(offset, Size.ToRect(), (ctx, o) =>
        {
            foreach (var c in Children) ctx.PaintChild(c, o + OffsetOf(c));
        });
        _scrollbar.Paint(context.Canvas, Size, _axis, _position, offset, MarkNeedsPaint);
    }

    public override bool HitTest(HitTestResult result, Offset position)
    {
        if (SizeOrNull is not { } size || !size.ToRect().Contains(position)) return false;
        if (_scrollbar.HitTest(size, _axis, _position, position))
        {
            result.Add(this);
            return true;
        }
        return base.HitTest(result, position);
    }

    public override void HandlePointerEvent(PointerEvent e, HitTestEntry entry) => _scrollbar.Handle(e, Size, _axis, _position);

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }
}

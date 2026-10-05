using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Scroll offset and extents for one scrollable, plus fling and animated scrolling.</summary>
public sealed class ScrollPosition
{
    static readonly TimeSpan WheelDuration = TimeSpan.FromMilliseconds(180);
    static readonly float FlingDrag = MathF.Exp(-3.2f);

    int _activity; // bumps on every new activity so stale frame callbacks stop
    float _wheelTarget;
    int _wheelActivity = -1;

    public float Pixels { get; private set; }
    public float MaxScrollExtent { get; private set; }
    public float ViewportExtent { get; private set; }
    public bool CanScroll => MaxScrollExtent > 0;
    public TimeSpan LastChange { get; private set; } = TimeSpan.FromHours(-1);

    /// <summary>True while the scrollbar owns the pointer, so the drag recognizer underneath ignores it.</summary>
    public bool ScrollbarActive { get; set; }

    public event Action? Changed;

    public void ApplyContentDimensions(float viewportExtent, float maxScrollExtent)
    {
        ViewportExtent = viewportExtent;
        MaxScrollExtent = Math.Max(0, maxScrollExtent);
        if (Pixels > MaxScrollExtent) Pixels = MaxScrollExtent;
    }

    /// <summary>Moves to <paramref name="value"/> (clamped). Returns how far it actually moved.</summary>
    public float JumpTo(float value)
    {
        value = Math.Clamp(value, 0, MaxScrollExtent);
        float moved = value - Pixels;
        if (moved == 0) return 0;
        Pixels = value;
        LastChange = WidgetsBinding.Instance.Now;
        Changed?.Invoke();
        return moved;
    }

    public void StopActivity() => _activity++;

    /// <summary>Adjusts the offset during layout (variable-height lists re-anchor as items are measured) without notifying.</summary>
    internal void CorrectTo(float value) => Pixels = Math.Clamp(value, 0, MaxScrollExtent);

    /// <summary>
    /// Scrolls by a wheel notch with a short eased animation. Notches arriving mid-animation accumulate onto the
    /// pending target. Returns false if the scrollable is already at that edge, so an outer one can take over.
    /// </summary>
    public bool ScrollBy(float delta)
    {
        float basis = _wheelActivity == _activity ? _wheelTarget : Pixels;
        float target = Math.Clamp(basis + delta, 0, MaxScrollExtent);
        if (target == basis) return false;
        AnimateTo(target, WheelDuration, Curves.EaseOut);
        _wheelTarget = target;
        _wheelActivity = _activity;
        return true;
    }

    /// <summary>Continues scrolling at <paramref name="velocity"/> px/s, slowing with friction.</summary>
    public void Fling(float velocity)
    {
        StopActivity();
        if (Math.Abs(velocity) < 50) return;
        int id = _activity;
        // Velocity halves roughly every 0.2s; the scroll stops once it falls below 25 px/s.
        var simulation = new FrictionSimulation(FlingDrag, Pixels, Math.Clamp(velocity, -8000, 8000))
            { Tolerance = new Tolerance(1, 25) };
        TimeSpan? start = null;

        void Step(TimeSpan now)
        {
            if (id != _activity) return;
            start ??= now;
            float seconds = (float)(now - start.Value).TotalSeconds;
            float wanted = simulation.X(seconds);
            JumpTo(wanted);
            bool hitEdge = wanted < 0 || wanted > MaxScrollExtent;
            if (simulation.IsDone(seconds) || hitEdge) return;
            WidgetsBinding.Instance.ScheduleFrameCallback(Step);
        }
        WidgetsBinding.Instance.ScheduleFrameCallback(Step);
    }

    public void AnimateTo(float target, TimeSpan duration, Curve? curve = null)
    {
        StopActivity();
        int id = _activity;
        float from = Pixels;
        target = Math.Clamp(target, 0, MaxScrollExtent);
        curve ??= Curves.EaseOut;
        TimeSpan? start = null;

        void Step(TimeSpan now)
        {
            if (id != _activity) return;
            start ??= now;
            float t = duration <= TimeSpan.Zero ? 1 : Math.Clamp((float)((now - start.Value) / duration), 0, 1);
            JumpTo(from + (target - from) * curve.Transform(t));
            if (t < 1) WidgetsBinding.Instance.ScheduleFrameCallback(Step);
        }
        WidgetsBinding.Instance.ScheduleFrameCallback(Step);
    }
}

public sealed class ScrollController
{
    public ScrollPosition Position { get; } = new();
    public float Offset => Position.Pixels;
    public float MaxScrollExtent => Position.MaxScrollExtent;

    public void JumpTo(float offset) { Position.StopActivity(); Position.JumpTo(offset); }
    public void AnimateTo(float offset, TimeSpan duration, Curve? curve = null) => Position.AnimateTo(offset, duration, curve);
}

/// <summary>Handles wheel and drag input for a scrollable and builds its viewport.</summary>
public sealed class Scrollable(Axis axis, Func<BuildContext, ScrollPosition, Widget> viewportBuilder,
    ScrollController? controller = null, Key? key = null) : StatefulWidget(key)
{
    internal Axis Axis => axis;
    internal ScrollController? Controller => controller;
    internal Func<BuildContext, ScrollPosition, Widget> ViewportBuilder => viewportBuilder;
    public override State CreateState() => new ScrollableState();
}

sealed class ScrollableState : State<Scrollable>
{
    ScrollController? _owned;
    ScrollPosition Position => (Widget.Controller ?? (_owned ??= new ScrollController())).Position;

    void OnWheel(PointerEvent e)
    {
        float delta = Widget.Axis == Axis.Vertical
            ? (e.ScrollDelta.Dy != 0 ? e.ScrollDelta.Dy : e.ScrollDelta.Dx)
            : (e.ScrollDelta.Dx != 0 ? e.ScrollDelta.Dx : e.ScrollDelta.Dy);
        // Only consume the wheel if this scrollable can move, so an outer one can take over at the edge.
        if (Position.ScrollBy(delta)) PointerSignal.Consume();
    }

    // The scrollbar can own the pointer while this drag recognizer (which may win the arena at pointer-down) is
    // also tracking it; remember per drag so the scrollbar's own release cannot let the content fling.
    bool _scrollbarDrag;

    void DragStart(DragDetails _)
    {
        _scrollbarDrag = Position.ScrollbarActive;
        if (!_scrollbarDrag) Position.StopActivity();
    }

    void DragUpdate(DragDetails d)
    {
        if (_scrollbarDrag) return;
        Position.JumpTo(Position.Pixels - (Widget.Axis == Axis.Vertical ? d.Delta.Dy : d.Delta.Dx));
    }

    void DragEnd(DragDetails d)
    {
        bool ignore = _scrollbarDrag;
        _scrollbarDrag = false;
        if (!ignore) Position.Fling(-(Widget.Axis == Axis.Vertical ? d.Velocity.Dy : d.Velocity.Dx));
    }

    public override Widget Build(BuildContext context)
    {
        bool v = Widget.Axis == Axis.Vertical;
        return new Listener(onPointerScroll: OnWheel, behavior: HitTestBehavior.Translucent,
            child: new GestureDetector(
                onVerticalDragStart: v ? DragStart : null, onVerticalDragUpdate: v ? DragUpdate : null, onVerticalDragEnd: v ? DragEnd : null,
                onHorizontalDragStart: v ? null : DragStart, onHorizontalDragUpdate: v ? null : DragUpdate, onHorizontalDragEnd: v ? null : DragEnd,
                behavior: HitTestBehavior.Translucent,
                child: Widget.ViewportBuilder(context, Position)));
    }
}

public sealed class SingleChildScrollView(Widget child, Axis scrollDirection = Axis.Vertical, ScrollController? controller = null,
    EdgeInsets? padding = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new Scrollable(scrollDirection, (_, position) =>
            new Viewport(scrollDirection, position, padding is { } p ? new Padding(p, child) : child), controller);
}

public sealed class Viewport(Axis axis, ScrollPosition position, Widget? child, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderViewport(axis, position);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderViewport)ro).Update(axis, position);
}

/// <summary>
/// Scrollbar geometry, painting and pointer handling shared by scrollable render objects. The thumb can be dragged
/// and the track clicked to page; it stays visible while the pointer is over the gutter or a drag is in progress.
/// </summary>
sealed class ScrollbarInteraction
{
    const float Thickness = 6, HoverThickness = 10, Margin = 2, Gutter = 14, Visible = 1.0f, Fade = 0.3f, MinThumb = 24;

    bool _pressed, _dragging;
    float _grab;

    static float Viewport(Size size, Axis axis) => axis == Axis.Vertical ? size.Height : size.Width;

    static (float start, float length) Thumb(Size size, Axis axis, ScrollPosition p)
    {
        float viewport = Viewport(size, axis);
        float total = viewport + p.MaxScrollExtent;
        float length = Math.Min(viewport, Math.Max(MinThumb, viewport * viewport / total));
        float travel = Math.Max(0, viewport - length - Margin * 2);
        float start = Margin + (p.MaxScrollExtent <= 0 ? 0 : travel * p.Pixels / p.MaxScrollExtent);
        return (start, length);
    }

    static bool InGutter(Size size, Axis axis, Offset local) => axis == Axis.Vertical
        ? local.Dx >= size.Width - Gutter && local.Dx < size.Width
        : local.Dy >= size.Height - Gutter && local.Dy < size.Height;

    static float Main(Offset o, Axis axis) => axis == Axis.Vertical ? o.Dy : o.Dx;

    /// <summary>The gutter swallows pointer input while the content can scroll.</summary>
    public bool HitTest(Size size, Axis axis, ScrollPosition p, Offset local) => p.CanScroll && InGutter(size, axis, local);

    public void Handle(PointerEvent e, Size size, Axis axis, ScrollPosition p)
    {
        float main = Main(e.LocalPosition, axis);
        switch (e.Kind)
        {
            // Only presses in the gutter belong to the scrollbar; the viewport also receives presses on its content.
            case PointerEventKind.Down when InGutter(size, axis, e.LocalPosition):
                _pressed = true;
                p.ScrollbarActive = true;
                p.StopActivity();
                var (start, length) = Thumb(size, axis, p);
                if (main >= start && main <= start + length)
                {
                    _dragging = true;
                    _grab = main - start;
                }
                else
                {
                    // Clicking the track pages toward the click.
                    float page = Viewport(size, axis) * 0.9f;
                    p.AnimateTo(p.Pixels + (main < start ? -page : page), TimeSpan.FromMilliseconds(200));
                }
                break;
            case PointerEventKind.Move when _dragging:
                var (_, len) = Thumb(size, axis, p);
                float travel = Math.Max(1, Viewport(size, axis) - len - Margin * 2);
                p.StopActivity();
                p.JumpTo((main - _grab - Margin) / travel * p.MaxScrollExtent);
                break;
            case PointerEventKind.Up or PointerEventKind.Cancel when _pressed:
                _pressed = false;
                _dragging = false;
                p.ScrollbarActive = false;
                break;
        }
    }

    public void Paint(SKCanvas canvas, Size size, Axis axis, ScrollPosition p, Offset offset, Action requestRepaint)
    {
        if (!p.CanScroll) return;
        var local = WidgetsBinding.Instance.Gestures.PointerPosition - offset;
        bool hovered = size.ToRect().Contains(local) && InGutter(size, axis, local);
        bool active = _dragging || hovered;

        float alpha = 1;
        if (!active)
        {
            float age = (float)(WidgetsBinding.Instance.Now - p.LastChange).TotalSeconds;
            if (age > Visible + Fade) return;
            alpha = age <= Visible ? 1 : 1 - (age - Visible) / Fade;
            // Keep frames coming until the fade finishes.
            WidgetsBinding.Instance.ScheduleFrameCallback(_ => requestRepaint());
        }

        var (pos, length) = Thumb(size, axis, p);
        float thickness = active ? HoverThickness : Thickness;
        float near = Margin + (HoverThickness - thickness) / 2;
        var rect = axis == Axis.Vertical
            ? new SKRect(offset.Dx + size.Width - thickness - near, offset.Dy + pos, offset.Dx + size.Width - near, offset.Dy + pos + length)
            : new SKRect(offset.Dx + pos, offset.Dy + size.Height - thickness - near, offset.Dx + pos + length, offset.Dy + size.Height - near);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)((active ? 150 : 100) * alpha)), IsAntialias = true };
        canvas.DrawRoundRect(rect, thickness / 2, thickness / 2, paint);
    }
}

/// <summary>A render object that scrolls its content, exposed so focus can bring a widget into view.</summary>
interface IScrollViewport
{
    Axis ScrollAxis { get; }
    ScrollPosition Position { get; }

    /// <summary>How far children are shifted at paint time beyond their layout offsets (zero when the offsets already include scrolling).</summary>
    Offset PaintShift { get; }
}

/// <summary>Shows a window onto a larger child, shifted by the scroll position.</summary>
public sealed class RenderViewport : RenderObjectWithChildBox, IScrollViewport
{
    Axis _axis;
    ScrollPosition _position;
    readonly ScrollbarInteraction _scrollbar = new();

    public RenderViewport(Axis axis, ScrollPosition position)
    {
        _axis = axis;
        _position = position;
    }

    public void Update(Axis axis, ScrollPosition position)
    {
        if (_axis == axis && ReferenceEquals(_position, position)) return;
        if (Owner is not null) _position.Changed -= OnScroll;
        _axis = axis;
        _position = position;
        if (Owner is not null) _position.Changed += OnScroll;
        MarkNeedsLayout();
    }

    void OnScroll() => MarkNeedsPaint();

    Axis IScrollViewport.ScrollAxis => _axis;
    ScrollPosition IScrollViewport.Position => _position;
    Offset IScrollViewport.PaintShift => ChildOffset;

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

    bool Vertical => _axis == Axis.Vertical;

    protected override void PerformLayout()
    {
        var c = Constraints;
        if (Child is not { } child)
        {
            Size = c.Biggest.Equals(default) ? Size.Zero : c.Constrain(Size.Zero);
            _position.ApplyContentDimensions(0, 0);
            return;
        }

        child.Layout(Vertical
            ? new BoxConstraints(c.MinWidth, c.MaxWidth, 0, float.PositiveInfinity)
            : new BoxConstraints(0, float.PositiveInfinity, c.MinHeight, c.MaxHeight));

        float viewportMain = Vertical
            ? (c.HasBoundedHeight ? c.MaxHeight : child.Size.Height)
            : (c.HasBoundedWidth ? c.MaxWidth : child.Size.Width);
        float cross = Vertical
            ? (c.HasBoundedWidth ? c.MaxWidth : child.Size.Width)
            : (c.HasBoundedHeight ? c.MaxHeight : child.Size.Height);
        Size = c.Constrain(Vertical ? new Size(cross, viewportMain) : new Size(viewportMain, cross));

        float viewport = Vertical ? Size.Height : Size.Width;
        float content = Vertical ? child.Size.Height : child.Size.Width;
        _position.ApplyContentDimensions(viewport, content - viewport);
    }

    Offset ChildOffset => Vertical ? new Offset(0, -_position.Pixels) : new Offset(-_position.Pixels, 0);

    public override void Paint(PaintingContext context, Offset offset)
    {
        if (Child is not { } child) return;
        context.PushClipRect(offset, Size.ToRect(), (ctx, o) => ctx.PaintChild(child, o + ChildOffset));
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

    protected override bool HitTestChildren(HitTestResult result, Offset position) =>
        Child is { } c && result.AddWithPaintOffset(ChildOffset, position, c.HitTest);

    public override float MinIntrinsicWidth(float h) => Child?.MinIntrinsicWidth(h) ?? 0;
    public override float MaxIntrinsicWidth(float h) => Child?.MaxIntrinsicWidth(h) ?? 0;
    public override float MinIntrinsicHeight(float w) => Child?.MinIntrinsicHeight(w) ?? 0;
    public override float MaxIntrinsicHeight(float w) => Child?.MaxIntrinsicHeight(w) ?? 0;
}

/// <summary>A scrolling list. <c>new ListView(children)</c> or <c>ListView.Builder(count, builder, itemExtent)</c> for lazy lists.</summary>
public sealed class ListView : StatelessWidget
{
    readonly IReadOnlyList<Widget>? _children;
    readonly int _itemCount;
    readonly Func<BuildContext, int, Widget>? _builder;
    readonly float? _itemExtent;
    readonly Axis _axis;
    readonly ScrollController? _controller;
    readonly EdgeInsets? _padding;

    public ListView(IReadOnlyList<Widget> children, Axis scrollDirection = Axis.Vertical, ScrollController? controller = null,
        EdgeInsets? padding = null, Key? key = null) : base(key)
    {
        _children = children; _axis = scrollDirection; _controller = controller; _padding = padding;
    }

    ListView(int itemCount, Func<BuildContext, int, Widget> builder, float? itemExtent, Axis axis, ScrollController? controller,
        EdgeInsets? padding, Key? key) : base(key)
    {
        _itemCount = itemCount; _builder = builder; _itemExtent = itemExtent; _axis = axis; _controller = controller; _padding = padding;
    }

    /// <summary>
    /// Builds items on demand. Pass <paramref name="itemExtent"/> when every item has the same main-axis size (fastest and exact); otherwise
    /// items are measured as they scroll into view and unmeasured ones are estimated from the average.
    /// </summary>
    public static ListView Builder(int itemCount, Func<BuildContext, int, Widget> itemBuilder, float? itemExtent = null,
        Axis scrollDirection = Axis.Vertical, ScrollController? controller = null, EdgeInsets? padding = null, Key? key = null) =>
        new(itemCount, itemBuilder, itemExtent, scrollDirection, controller, padding, key);

    public override Widget Build(BuildContext context)
    {
        if (_children is not null)
            return new SingleChildScrollView(
                new Flex(_axis, _children, MainAxisAlignment.Start, MainAxisSize.Min, CrossAxisAlignment.Stretch),
                _axis, _controller, _padding);

        return new Scrollable(_axis, (_, position) =>
            new LazyViewport(_axis, position, _itemCount, _builder!, _itemExtent, _padding ?? EdgeInsets.Zero), _controller);
    }
}

public sealed class LazyListParentData : BoxParentData
{
    public int Index;
}

public sealed class LazyViewport(Axis axis, ScrollPosition position, int itemCount, Func<BuildContext, int, Widget> builder,
    float? itemExtent, EdgeInsets padding, Key? key = null) : RenderObjectWidget(key)
{
    internal Axis Axis => axis;
    internal ScrollPosition Position => position;
    internal int ItemCount => itemCount;
    internal Func<BuildContext, int, Widget> Builder => builder;
    internal float? ItemExtent => itemExtent;
    internal EdgeInsets Padding => padding;

    public override Element CreateElement() => new LazyViewportElement(this);
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLazyViewport();
}

sealed class LazyViewportElement : RenderObjectElement
{
    readonly Dictionary<int, Element> _children = new();
    int _version;
    int _builtVersion = -1;

    public LazyViewportElement(LazyViewport widget) : base(widget) { }

    LazyViewport W => (LazyViewport)Widget;
    RenderLazyViewport RO => (RenderLazyViewport)RenderObject;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        Configure();
        RO.BuildRange = BuildRange;
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _version++;
        Configure();
    }

    void Configure() => RO.Configure(W.Axis, W.Position, W.ItemCount, W.ItemExtent, W.Padding);

    void BuildRange(int first, int last)
    {
        bool rebuild = _builtVersion != _version;
        _builtVersion = _version;

        foreach (var idx in _children.Keys.Where(i => i < first || i > last || i >= W.ItemCount).ToList())
        {
            _children[idx].Unmount();
            _children.Remove(idx);
        }

        for (int i = first; i <= last && i < W.ItemCount; i++)
        {
            bool have = _children.TryGetValue(i, out var existing);
            if (have && !rebuild) continue;
            var widget = new KeyedSubtree(new ValueKey<int>(i), new Builder(ctx => W.Builder(ctx, i)));
            var child = UpdateChildPublic(existing, widget);
            if (child is not null) _children[i] = child;
        }
        ResyncChildren();
    }

    Element? UpdateChildPublic(Element? existing, Widget widget) => UpdateChild(existing, widget);

    internal override void ResyncChildren()
    {
        var boxes = new List<RenderBox>();
        foreach (var (index, element) in _children.OrderBy(kv => kv.Key))
        {
            if (element.FindRenderObject() is not RenderBox box) continue;
            if (box.ParentData is not LazyListParentData pd) box.ParentData = pd = new LazyListParentData();
            pd.Index = index;
            boxes.Add(box);
        }
        RO.SetChildren(boxes);
    }

    public override void VisitChildren(Action<Element> visitor)
    {
        foreach (var c in _children.Values) visitor(c);
    }
}

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

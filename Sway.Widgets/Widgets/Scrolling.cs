using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Scroll offset and extents for one scrollable, plus fling and animated scrolling.</summary>
public sealed class ScrollPosition
{
    float _velocity;
    int _activity; // bumps on every new activity so stale frame callbacks stop

    public float Pixels { get; private set; }
    public float MaxScrollExtent { get; private set; }
    public float ViewportExtent { get; private set; }
    public bool CanScroll => MaxScrollExtent > 0;
    public TimeSpan LastChange { get; private set; } = TimeSpan.FromHours(-1);

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

    /// <summary>Continues scrolling at <paramref name="velocity"/> px/s, slowing with friction.</summary>
    public void Fling(float velocity)
    {
        StopActivity();
        if (Math.Abs(velocity) < 50) return;
        _velocity = Math.Clamp(velocity, -8000, 8000);
        int id = _activity;
        TimeSpan? last = null;

        void Step(TimeSpan now)
        {
            if (id != _activity) return;
            float dt = last is null ? 1 / 60f : Math.Clamp((float)(now - last.Value).TotalSeconds, 0.001f, 0.05f);
            last = now;
            float moved = JumpTo(Pixels + _velocity * dt);
            _velocity *= MathF.Exp(-3.2f * dt);
            if (Math.Abs(_velocity) < 25 || (moved == 0 && _velocity != 0)) return;
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
        Position.StopActivity();
        // Only consume the wheel if this scrollable moved, so an outer one can take over at the edge.
        if (Position.JumpTo(Position.Pixels + delta) != 0) PointerSignal.Consume();
    }

    void DragStart(DragDetails _) => Position.StopActivity();
    void DragUpdate(DragDetails d) =>
        Position.JumpTo(Position.Pixels - (Widget.Axis == Axis.Vertical ? d.Delta.Dy : d.Delta.Dx));
    void DragEnd(DragDetails d) =>
        Position.Fling(-(Widget.Axis == Axis.Vertical ? d.Velocity.Dy : d.Velocity.Dx));

    public override Widget Build(BuildContext context)
    {
        bool v = Widget.Axis == Axis.Vertical;
        return new Listener(onPointerScroll: OnWheel,
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

/// <summary>Shared scrollbar painting for scrollable render objects.</summary>
static class ScrollbarPainter
{
    const float Thickness = 6, Margin = 2, Visible = 1.0f, Fade = 0.3f;

    public static void Paint(SKCanvas canvas, Size size, Axis axis, ScrollPosition p, Offset offset, Action requestRepaint)
    {
        if (!p.CanScroll) return;
        var now = WidgetsBinding.Instance.Now;
        float age = (float)(now - p.LastChange).TotalSeconds;
        if (age > Visible + Fade) return;
        float alpha = age <= Visible ? 1 : 1 - (age - Visible) / Fade;
        // Keep frames coming until the fade finishes.
        WidgetsBinding.Instance.ScheduleFrameCallback(_ => requestRepaint());

        float viewport = axis == Axis.Vertical ? size.Height : size.Width;
        float total = viewport + p.MaxScrollExtent;
        float thumb = Math.Max(24, viewport * viewport / total);
        float travel = viewport - thumb - Margin * 2;
        float pos = Margin + (p.MaxScrollExtent <= 0 ? 0 : travel * p.Pixels / p.MaxScrollExtent);

        var rect = axis == Axis.Vertical
            ? new SKRect(offset.Dx + size.Width - Thickness - Margin, offset.Dy + pos, offset.Dx + size.Width - Margin, offset.Dy + pos + thumb)
            : new SKRect(offset.Dx + pos, offset.Dy + size.Height - Thickness - Margin, offset.Dx + pos + thumb, offset.Dy + size.Height - Margin);
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(100 * alpha)), IsAntialias = true };
        canvas.DrawRoundRect(rect, Thickness / 2, Thickness / 2, paint);
    }
}

/// <summary>Shows a window onto a larger child, shifted by the scroll position.</summary>
public sealed class RenderViewport : RenderObjectWithChildBox
{
    Axis _axis;
    ScrollPosition _position;

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
        ScrollbarPainter.Paint(context.Canvas, Size, _axis, _position, offset, MarkNeedsPaint);
    }

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
    /// Builds items on demand. Items must share one main-axis size: pass <paramref name="itemExtent"/>, or the first item's
    /// size is used for all of them.
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

/// <summary>Viewport that creates only the visible fixed-extent items, building them during layout.</summary>
public sealed class RenderLazyViewport : RenderBoxContainer
{
    Axis _axis;
    ScrollPosition _position = new();
    int _itemCount;
    float? _itemExtent;
    float _measuredExtent;
    EdgeInsets _padding;

    public Action<int, int>? BuildRange;

    public void Configure(Axis axis, ScrollPosition position, int itemCount, float? itemExtent, EdgeInsets padding)
    {
        bool changed = _axis != axis || _itemCount != itemCount || _itemExtent != itemExtent || _padding != padding;
        if (!ReferenceEquals(_position, position))
        {
            if (Owner is not null) _position.Changed -= OnScroll;
            _position = position;
            if (Owner is not null) _position.Changed += OnScroll;
            changed = true;
        }
        _axis = axis; _itemCount = itemCount; _itemExtent = itemExtent; _padding = padding;
        if (changed) _measuredExtent = 0;
        MarkNeedsLayout();
    }

    void OnScroll() => MarkNeedsLayout();

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

        BoxConstraints ItemConstraints(float extent) => Vertical
            ? new BoxConstraints(innerCross, innerCross, extent, extent)
            : new BoxConstraints(extent, extent, innerCross, innerCross);

        if (_itemCount == 0)
        {
            BuildRange?.Invoke(0, -1);
            _position.ApplyContentDimensions(viewport, 0);
            return;
        }

        float extent = _itemExtent ?? _measuredExtent;
        if (extent <= 0)
        {
            // Measure the first item to learn the shared item size.
            BuildRange?.Invoke(0, 0);
            if (Children.Count > 0)
            {
                Children[0].Layout(Vertical ? new BoxConstraints(innerCross, innerCross, 0, float.PositiveInfinity)
                                            : new BoxConstraints(0, float.PositiveInfinity, innerCross, innerCross));
                _measuredExtent = extent = Math.Max(1, Vertical ? Children[0].Size.Height : Children[0].Size.Width);
            }
            else extent = 1;
        }

        float total = padStart + extent * _itemCount + padEnd;
        _position.ApplyContentDimensions(viewport, total - viewport);

        float scroll = _position.Pixels;
        int first = Math.Max(0, (int)MathF.Floor((scroll - padStart) / extent));
        int last = Math.Min(_itemCount - 1, (int)MathF.Ceiling((scroll + viewport - padStart) / extent) - 1);
        BuildRange?.Invoke(first, last);

        foreach (var child in Children)
        {
            var pd = (LazyListParentData)child.ParentData!;
            child.Layout(ItemConstraints(extent));
            float main = padStart + pd.Index * extent - scroll;
            pd.Offset = Vertical ? new Offset(crossStart, main) : new Offset(main, crossStart);
        }
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        context.PushClipRect(offset, Size.ToRect(), (ctx, o) =>
        {
            foreach (var c in Children) ctx.PaintChild(c, o + OffsetOf(c));
        });
        ScrollbarPainter.Paint(context.Canvas, Size, _axis, _position, offset, MarkNeedsPaint);
    }

    protected override bool HitTestChildren(HitTestResult result, Offset position)
    {
        for (int i = Children.Count - 1; i >= 0; i--)
            if (HitTestChild(Children[i], result, position)) return true;
        return false;
    }
}

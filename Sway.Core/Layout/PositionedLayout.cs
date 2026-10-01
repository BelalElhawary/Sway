using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>
/// position: relative / absolute / fixed, and scrollable overflow extents.
/// Absolute boxes are taken out of flow while their parent lays out; they are placed once the
/// positioned ancestor that acts as their containing block has a final size.
/// Sticky is treated as static for now.
/// </summary>
public sealed partial class LayoutEngine
{
    sealed record PendingAbsolute(ElementNode Element, ElementNode Container, float StaticX, float StaticY);

    readonly List<PendingAbsolute> _pending = new();
    float _viewportWidth, _viewportHeight;

    static bool IsOutOfFlow(Node node) => node is ElementNode { Style.IsOutOfFlow: true };

    /// <summary>Remembers an out-of-flow element and where it would have sat in normal flow.</summary>
    void RegisterOutOfFlow(ElementNode el, float staticX, float staticY)
    {
        ElementNode container;
        if (el.Style.Position == Position.Fixed)
        {
            container = RootOf(el);
        }
        else
        {
            var ancestor = el.ParentElement;
            while (ancestor is not null && ancestor.Style.Position == Position.Static && ancestor.Parent is not null)
                ancestor = ancestor.ParentElement;
            container = ancestor ?? RootOf(el);
        }

        // Layout results that depend on where an out-of-flow box ends up cannot be reused from cache.
        for (var a = el.ParentElement; a is not null; a = a.ParentElement) a.ContainsOutOfFlow = true;

        // Measuring passes lay subtrees out more than once; the latest static position wins.
        _pending.RemoveAll(p => p.Element == el);
        _pending.Add(new PendingAbsolute(el, container, staticX, staticY));
        // The box keeps its previous rectangle until it is placed at the end of this pass. Clearing it would
        // corrupt a cached subtree, which only moves its stored rectangles.
    }

    static ElementNode RootOf(ElementNode el)
    {
        var node = el;
        while (node.ParentElement is { } parent) node = parent;
        return node;
    }

    /// <summary>Static positions for out-of-flow children of flex and grid containers (their content origin).</summary>
    void RegisterOutOfFlowChildren(ElementNode container, float x, float y)
    {
        foreach (var child in container.PhysicalChildren)
            if (child is ElementNode { Style.Display: not Display.None } e && e.Style.IsOutOfFlow)
                RegisterOutOfFlow(e, x, y);
    }

    void ApplyRelativeOffset(ElementNode el, float containingWidth)
    {
        el.RelativeOffset = default;
        var st = el.Style;
        if (st.Position != Position.Relative) return;

        float? left = st.Inset[L].Resolve(containingWidth), right = st.Inset[R].Resolve(containingWidth);
        float? top = st.Inset[T].Resolve(containingWidth), bottom = st.Inset[B].Resolve(containingWidth);
        float dx = left ?? (right is { } r ? -r : 0);
        float dy = top ?? (bottom is { } b ? -b : 0);
        if (dx == 0 && dy == 0) return;

        Translate(el, dx, dy);
        el.RelativeOffset = new SKPoint(dx, dy);
    }

    /// <summary>Places every absolute box whose containing block is <paramref name="container"/>.</summary>
    void LayoutPendingFor(ElementNode container)
    {
        while (true)
        {
            var ready = _pending.Where(p => p.Container == container).ToList();
            if (ready.Count == 0) return;
            _pending.RemoveAll(p => p.Container == container);
            foreach (var p in ready) LayoutAbsolute(p);
        }
    }

    void LayoutAbsolute(PendingAbsolute pending)
    {
        var el = pending.Element;
        var st = el.Style;

        // Fixed boxes and boxes anchored to the root use the viewport; others use the padding box.
        bool useViewport = st.Position == Position.Fixed || pending.Container.Parent is null;
        var cb = useViewport ? new SKRect(0, 0, _viewportWidth, _viewportHeight) : pending.Container.PaddingBox;
        float cbW = cb.Width, cbH = cb.Height;

        float? left = st.Inset[L].Resolve(cbW), right = st.Inset[R].Resolve(cbW);
        float? top = st.Inset[T].Resolve(cbH), bottom = st.Inset[B].Resolve(cbH);
        float ml = st.Margin[L].Resolve(cbW) ?? 0, mr = st.Margin[R].Resolve(cbW) ?? 0;
        float mt = st.Margin[T].Resolve(cbW) ?? 0, mb = st.Margin[B].Resolve(cbW) ?? 0;
        float hChrome = HorizontalChromeAt(st, cbW), vChrome = VerticalChrome(st, cbW);

        float? width = st.Width.Resolve(cbW);
        if (width is { } w0) width = st.BorderBox ? Math.Max(w0, hChrome) : w0 + hChrome;
        else if (left is { } l && right is { } r) width = Math.Max(0, cbW - l - r - ml - mr);
        if (width is { } w1) width = ClampBorderSize(w1, hChrome, st.MinWidth, st.MaxWidth, cbW, st.BorderBox);

        float? height = st.Height.Unit == LengthUnit.Percent ? st.Height.Value / 100f * cbH : st.Height.Resolve(0);
        if (height is { } h0) height = st.BorderBox ? Math.Max(h0, vChrome) : h0 + vChrome;
        else if (top is { } t && bottom is { } b) height = Math.Max(0, cbH - t - b - mt - mb);

        LayoutBlock(el, 0, 0, cbW, shrink: width is null, forcedWidth: width, forcedHeight: height);

        var size = el.BorderRect;
        float x = left is { } lx ? cb.Left + lx + ml
            : right is { } rx ? cb.Right - rx - mr - size.Width
            : pending.StaticX + ml;
        float y = top is { } ty ? cb.Top + ty + mt
            : bottom is { } by ? cb.Bottom - by - mb - size.Height
            : pending.StaticY + mt;

        Translate(el, x - size.Left, y - size.Top);
    }

    // ---- scrollable overflow ----

    /// <summary>
    /// One post-order pass that records each element's visual bounds (used to cull what is off screen)
    /// and works out how far each clipping element can scroll, clamping its current offset.
    /// Returns the bounds of what the element paints as seen by an ancestor.
    /// </summary>
    SKRect? ComputeExtents(ElementNode el)
    {
        // Untouched subtrees keep their bounds (moved along by Translate) and scroll limits.
        if (!el.ExtentsDirty) return el.HasVisualBounds ? el.VisualBounds : null;
        el.ExtentsDirty = false;

        SKRect? content = null;
        foreach (var child in el.PhysicalChildren)
        {
            if (child is TextNode text) { Merge(ref content, TextBounds(text)); continue; }

            var e = (ElementNode)child;
            if (e.Style.Display == Display.None) continue;
            var bounds = ComputeExtents(e);
            // Fixed boxes do not scroll with their parent, so they never extend its scrollable area.
            if (e.Style.Position != Position.Fixed) Merge(ref content, bounds);
        }

        if (el.ClipsContent)
        {
            var pad = el.PaddingBox;
            SKRect? extent = content;
            if (el.Parent is null) Merge(ref extent, el.BorderRect);

            float padRight = el.Parent is null ? 0 : el.Style.Padding[R].Resolve(el.ContentRect.Width) ?? 0;
            float padBottom = el.Parent is null ? 0 : el.Style.Padding[B].Resolve(el.ContentRect.Width) ?? 0;

            el.MaxScrollX = Math.Max(0, (extent?.Right ?? pad.Left) + padRight - pad.Right);
            el.MaxScrollY = Math.Max(0, (extent?.Bottom ?? pad.Top) + padBottom - pad.Bottom);
            el.ScrollX = Math.Clamp(el.ScrollX, 0, el.MaxScrollX);
            el.ScrollY = Math.Clamp(el.ScrollY, 0, el.MaxScrollY);
        }
        else
        {
            el.MaxScrollX = el.MaxScrollY = el.ScrollX = el.ScrollY = 0;
        }

        SKRect? own = el.BorderRect.Width > 0 || el.BorderRect.Height > 0 ? el.BorderRect : null;
        // A clipping element hides its overflow from ancestors, so only its own box counts for them.
        SKRect? seen = own;
        if (!el.ClipsContent) Merge(ref seen, content);

        el.HasVisualBounds = seen is not null;
        el.VisualBounds = seen ?? default;
        return seen;
    }

    static SKRect? TextBounds(TextNode text)
    {
        if (text.Runs.Count == 0) return null;

        var metrics = FontCache.Get(text.ParentElement!.Style).Metrics;
        SKRect? bounds = null;
        foreach (var run in text.Runs)
            Merge(ref bounds, new SKRect(run.X, run.Baseline + metrics.Ascent, run.X + run.Width, run.Baseline + metrics.Descent));
        return bounds;
    }

    static void Merge(ref SKRect? target, SKRect? other)
    {
        if (other is not { } o) return;
        if (target is not { } t) { target = o; return; }
        target = new SKRect(Math.Min(t.Left, o.Left), Math.Min(t.Top, o.Top), Math.Max(t.Right, o.Right), Math.Max(t.Bottom, o.Bottom));
    }
}

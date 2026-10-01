using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Layout;
using Sway.Core.Styling;

namespace Sway.Core.Rendering;

public enum OpKind { Box, Text, PushScroll, PopScroll, PushTransform, PopTransform, PushEffects, PopEffects, Scrollbars, Popup }

/// <summary>What a pointer is over, plus the scroll offset to add to a window point to get layout space.</summary>
public readonly record struct HitResult(ElementNode Element, Affine Inverse)
{
    /// <summary>Maps a window point into the element's layout space, undoing scrolling and transforms.</summary>
    public SKPoint ToLocal(float x, float y) => Inverse.Map(x, y);
}

public readonly record struct PaintOp(OpKind Kind, ElementNode? Element = null, TextNode? Text = null);

/// <summary>
/// Flattens the tree into paint order following CSS stacking contexts. Both painting and hit
/// testing walk this list, so what is clickable always matches what is drawn.
/// </summary>
/// <remarks>
/// Approximation: sticky positioning and transforms do not create contexts yet. A positioned
/// element with z-index: auto is painted at z-index 0 together with its positioned descendants.
/// </remarks>
public static class DisplayListBuilder
{
    /// <param name="openSelect">A select whose dropdown is showing; it is drawn last, above everything.</param>
    public static List<PaintOp> Build(Document document, ElementNode? openSelect = null)
    {
        var ops = new List<PaintOp>();
        EmitContext(document.Root, ops, Everything);
        if (openSelect is not null) ops.Add(new PaintOp(OpKind.Popup, openSelect));
        return ops;
    }

    static readonly SKRect Everything = new(float.MinValue / 2, float.MinValue / 2, float.MaxValue / 2, float.MaxValue / 2);

    // Elements this far outside the visible region are still drawn, so shadows and outlines that
    // reach into view are not cut off.
    const float CullMargin = 160;

    static bool Overlaps(SKRect a, SKRect b) => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;

    /// <summary>The part of an element's content that can be on screen, in the content's own (unscrolled) coordinates.</summary>
    static SKRect VisibleInside(ElementNode el, SKRect visibleOutside)
    {
        if (el.Style.Transform is not null) return Everything; // the region would need mapping through the transform
        if (!el.ClipsContent) return visibleOutside;

        var pad = el.PaddingBox;
        if (el.Parent is not null)
        {
            if (!Overlaps(visibleOutside, pad)) return new SKRect(1e30f, 1e30f, -1e30f, -1e30f); // nothing visible inside (never overlaps anything)
            pad = new SKRect(Math.Max(pad.Left, visibleOutside.Left), Math.Max(pad.Top, visibleOutside.Top),
                Math.Min(pad.Right, visibleOutside.Right), Math.Min(pad.Bottom, visibleOutside.Bottom));
        }
        return new SKRect(pad.Left + el.ScrollX, pad.Top + el.ScrollY, pad.Right + el.ScrollX, pad.Bottom + el.ScrollY);
    }

    // Positioned boxes and translucent ones are painted in their own pass, not in normal flow.
    static bool IsEntry(ElementNode e) => e.Style.Position != Position.Static || HasEffects(e);

    static bool HasEffects(ElementNode e) =>
        e.Style.Opacity < 1 || e.Style.Transform is not null || e.Style.Filters.Count > 0;

    static bool CreatesContext(ElementNode e) =>
        e.Parent is null
        || HasEffects(e)
        || e.Style.Position == Position.Fixed
        || (e.Style.Position != Position.Static && e.Style.ZIndex.HasValue);

    static int ZOf(ElementNode e) => e.Style.ZIndex ?? 0;

    static void EmitContext(ElementNode ctx, List<PaintOp> ops, SKRect visibleOutside)
    {
        var visible = VisibleInside(ctx, visibleOutside);
        bool transformed = ctx.Style.Transform is not null;
        bool layer = ctx.Style.Opacity < 1 || ctx.Style.Filters.Count > 0;
        bool clips = ctx.ClipsContent;

        // The transform is outermost so that the effects layer is composited in the transformed space.
        if (transformed) ops.Add(new PaintOp(OpKind.PushTransform, ctx));
        if (layer) ops.Add(new PaintOp(OpKind.PushEffects, ctx));
        ops.Add(new PaintOp(OpKind.Box, ctx));
        if (clips) ops.Add(new PaintOp(OpKind.PushScroll, ctx));

        var entries = new List<ElementNode>();
        CollectEntries(ctx, entries);

        // Stable sorts keep tree order among equal z-index values.
        foreach (var e in entries.Where(e => ZOf(e) < 0).OrderBy(ZOf)) EmitEntry(e, ctx, clips, ops);
        EmitFlowChildren(ctx, ops, visible);
        foreach (var e in entries.Where(e => ZOf(e) == 0)) EmitEntry(e, ctx, clips, ops);
        foreach (var e in entries.Where(e => ZOf(e) > 0).OrderBy(ZOf)) EmitEntry(e, ctx, clips, ops);

        if (clips)
        {
            ops.Add(new PaintOp(OpKind.PopScroll));
            ops.Add(new PaintOp(OpKind.Scrollbars, ctx));
        }
        if (layer) ops.Add(new PaintOp(OpKind.PopEffects));
        if (transformed) ops.Add(new PaintOp(OpKind.PopTransform));
    }

    // Everything painted outside normal flow within this context, in tree order.
    static void CollectEntries(ElementNode parent, List<ElementNode> into)
    {
        foreach (var child in parent.PhysicalChildren)
        {
            if (child is not ElementNode e || e.Style.Display == Display.None) continue;
            if (IsEntry(e))
            {
                into.Add(e);
                if (CreatesContext(e)) continue; // its descendants belong to its own context
            }
            if (e.HasEntryDescendant) CollectEntries(e, into); // skip subtrees that certainly hold none
        }
    }

    static void EmitEntry(ElementNode entry, ElementNode ctx, bool ctxClips, List<PaintOp> ops)
    {
        bool isFixed = entry.Style.Position == Position.Fixed;

        // Fixed boxes ignore the scrolling of the context they sit in.
        if (isFixed && ctxClips) ops.Add(new PaintOp(OpKind.PopScroll));

        var chain = ClippingAncestors(entry, ctx);
        foreach (var a in chain) ops.Add(new PaintOp(OpKind.PushScroll, a));

        if (CreatesContext(entry))
        {
            EmitContext(entry, ops, Everything);
        }
        else
        {
            ops.Add(new PaintOp(OpKind.Box, entry));
            EmitClippedChildren(entry, ops, Everything);
        }

        for (int i = 0; i < chain.Count; i++) ops.Add(new PaintOp(OpKind.PopScroll));
        if (isFixed && ctxClips) ops.Add(new PaintOp(OpKind.PushScroll, ctx));
    }

    /// <summary>
    /// Ancestors (outermost first) whose clip and scroll apply to a positioned entry. An absolute box
    /// is only affected by ancestors at or above its containing block, not by those in between.
    /// </summary>
    static List<ElementNode> ClippingAncestors(ElementNode entry, ElementNode ctx)
    {
        var result = new List<ElementNode>();
        if (entry.Style.Position == Position.Fixed) return result;

        var path = new List<ElementNode>();
        for (var a = entry.ParentElement; a is not null && a != ctx; a = a.ParentElement) path.Add(a);
        path.Reverse(); // outermost first

        int limit = path.Count;
        if (entry.Style.Position == Position.Absolute)
        {
            int cb = path.FindLastIndex(a => a.Style.Position != Position.Static);
            limit = cb + 1; // none in the path means the containing block is the context itself or above
        }

        for (int i = 0; i < limit; i++)
            if (path[i].ClipsContent) result.Add(path[i]);
        return result;
    }

    static void EmitFlowChildren(ElementNode parent, List<PaintOp> ops, SKRect visible)
    {
        // Content known to lie wholly outside the visible region produces no ops at all.
        var window = new SKRect(visible.Left - CullMargin, visible.Top - CullMargin, visible.Right + CullMargin, visible.Bottom + CullMargin);

        foreach (var child in parent.PhysicalChildren)
        {
            if (child is TextNode text) { ops.Add(new PaintOp(OpKind.Text, Text: text)); continue; }

            var e = (ElementNode)child;
            if (e.Style.Display == Display.None || IsEntry(e)) continue;
            if (!e.HasVisualBounds || !Overlaps(e.VisualBounds, window)) continue;

            ops.Add(new PaintOp(OpKind.Box, e));
            EmitClippedChildren(e, ops, visible);
        }
    }

    static void EmitClippedChildren(ElementNode e, List<PaintOp> ops, SKRect visibleOutside)
    {
        if (e.ClipsContent)
        {
            ops.Add(new PaintOp(OpKind.PushScroll, e));
            EmitFlowChildren(e, ops, VisibleInside(e, visibleOutside));
            ops.Add(new PaintOp(OpKind.PopScroll));
            ops.Add(new PaintOp(OpKind.Scrollbars, e));
        }
        else
        {
            EmitFlowChildren(e, ops, VisibleInside(e, visibleOutside));
        }
    }

    // ---- hit testing ----

    /// <summary>
    /// Finds the topmost element under a window point by replaying the list, tracking the scroll and
    /// transform of each level so the point can be mapped into that level's layout space.
    /// </summary>
    public static HitResult HitTest(IReadOnlyList<PaintOp> ops, ElementNode root, float x, float y)
    {
        var current = Affine.Identity;   // layout space -> window
        var inverse = Affine.Identity;   // window -> layout space
        int clippedOut = 0;
        var stack = new Stack<(Affine current, Affine inverse, bool clipped)>();
        ElementNode? hit = null;
        var hitInverse = Affine.Identity;

        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case OpKind.PushScroll:
                {
                    var el = op.Element!;
                    var local = inverse.Map(x, y);
                    bool outside = !el.PaddingBox.Contains(local.X, local.Y);
                    if (outside) clippedOut++;
                    stack.Push((current, inverse, outside));

                    var shift = Affine.Translate(-el.ScrollX, -el.ScrollY);
                    current = Affine.Compose(current, shift);
                    inverse = Affine.Compose(Affine.Translate(el.ScrollX, el.ScrollY), inverse);
                    break;
                }
                case OpKind.PushTransform:
                {
                    var el = op.Element!;
                    var m = Affine.OfElement(el);
                    bool invertible = m.TryInvert(out var mInverse);
                    if (!invertible) clippedOut++; // a collapsed transform cannot be hit
                    stack.Push((current, inverse, !invertible));

                    current = Affine.Compose(current, m);
                    inverse = Affine.Compose(mInverse, inverse);
                    break;
                }
                case OpKind.PopScroll or OpKind.PopTransform:
                {
                    var (savedCurrent, savedInverse, clipped) = stack.Pop();
                    current = savedCurrent;
                    inverse = savedInverse;
                    if (clipped) clippedOut--;
                    break;
                }
                case OpKind.Box when clippedOut == 0:
                {
                    var el = op.Element!;
                    if (el.Style.PointerEventsNone || el.Style.VisibilityHidden) break;

                    var local = inverse.Map(x, y);
                    if (el.Parent is null || el.BorderRect.Contains(local.X, local.Y))
                    {
                        hit = el;
                        hitInverse = inverse;
                    }
                    break;
                }
                case OpKind.Text when clippedOut == 0:
                {
                    // Text is how inline elements (which have no box) become clickable.
                    var owner = op.Text!.ParentElement;
                    if (owner is null || owner.Style.PointerEventsNone || owner.Style.VisibilityHidden) break;

                    var metrics = FontCache.Get(owner.Style).Metrics;
                    var local = inverse.Map(x, y);
                    foreach (var run in op.Text.Runs)
                    {
                        if (local.X >= run.X && local.X <= run.X + run.Width
                            && local.Y >= run.Baseline + metrics.Ascent && local.Y <= run.Baseline + metrics.Descent)
                        {
                            hit = owner;
                            hitInverse = inverse;
                            break;
                        }
                    }
                    break;
                }
            }
        }
        return new HitResult(hit ?? root, hitInverse);
    }
}

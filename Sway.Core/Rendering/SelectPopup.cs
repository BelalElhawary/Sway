using SkiaSharp;
using Sway.Core.Dom;
using Sway.Core.Layout;

namespace Sway.Core.Rendering;

/// <summary>Geometry of the dropdown list shown for an open select. Rects are in layout space.</summary>
public readonly record struct PopupGeometry(
    SKRect Rect, float ItemHeight, int VisibleCount, float MaxScroll, List<Controls.SelectOption> Options);

public static class SelectPopup
{
    public const int MaxVisibleItems = 10;
    const float Border = 1;

    public static PopupGeometry Compute(ElementNode select, float viewportHeight)
    {
        var options = Controls.Options(select);
        var style = select.Style;
        float itemHeight = Math.Max(22, FontCache.LineHeight(style) + 8);
        int visible = Math.Clamp(options.Count, 1, MaxVisibleItems);
        float height = visible * itemHeight + Border * 2;

        float widest = options.Count == 0 ? 0 : options.Max(o => FontCache.Measure(o.Label, style));
        float width = Math.Max(select.BorderRect.Width, widest + 24);

        var (_, offsetY) = ScrollOffset(select);
        float top = select.BorderRect.Bottom;
        // Flip above the control when the list would run off the window and there is room above.
        if (top - offsetY + height > viewportHeight && select.BorderRect.Top - offsetY - height >= 0)
            top = select.BorderRect.Top - height;

        float left = style.Direction == Styling.Direction.Rtl ? select.BorderRect.Right - width : select.BorderRect.Left;
        var rect = new SKRect(left, top, left + width, top + height);
        float maxScroll = Math.Max(0, options.Count * itemHeight - (height - Border * 2));
        return new PopupGeometry(rect, itemHeight, visible, maxScroll, options);
    }

    /// <summary>How far the element's ancestors have scrolled it, i.e. layout space minus window space.</summary>
    public static (float x, float y) ScrollOffset(ElementNode el)
    {
        float x = 0, y = 0;
        for (var a = el.ParentElement; a is not null; a = a.ParentElement)
        {
            if (!a.ClipsContent) continue;
            x += a.ScrollX;
            y += a.ScrollY;
        }
        return (x, y);
    }

    /// <summary>The option under a window point, or -1. Coordinates are in window space.</summary>
    public static int OptionAt(PopupGeometry g, ElementNode select, float scroll, float x, float y)
    {
        var (ox, oy) = ScrollOffset(select);
        float lx = x + ox, ly = y + oy;
        if (!g.Rect.Contains(lx, ly)) return -1;

        int index = (int)Math.Floor((ly - g.Rect.Top - Border + scroll) / g.ItemHeight);
        return index >= 0 && index < g.Options.Count ? index : -1;
    }

    public static bool Contains(PopupGeometry g, ElementNode select, float x, float y)
    {
        var (ox, oy) = ScrollOffset(select);
        return g.Rect.Contains(x + ox, y + oy);
    }
}

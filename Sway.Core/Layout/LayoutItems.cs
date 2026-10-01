using Sway.Core.Dom;
using Sway.Core.Styling;

namespace Sway.Core.Layout;

/// <summary>Helpers shared by flex and grid layout: item collection, sizing queries and placement.</summary>
public sealed partial class LayoutEngine
{
    const int L = ComputedStyle.Left, R = ComputedStyle.Right, T = ComputedStyle.Top, B = ComputedStyle.Bottom;

    static readonly ComputedStyle DefaultItemStyle = ComputedStyle.CreateRoot();

    // Intrinsic widths and natural heights are recomputed many times while flex and grid containers
    // negotiate sizes, and nested containers multiply that work. Both are stable within one pass.
    readonly Dictionary<(ElementNode, bool), float> _widthCache = new();
    readonly Dictionary<(ElementNode, float), float> _heightCache = new();

    /// <summary>A flex or grid item: a child element, or a run of text that gets an anonymous box.</summary>
    sealed class LayoutItem
    {
        public required ElementNode Container { get; init; }
        public ElementNode? El { get; init; }
        public TextNode? Text { get; init; }

        /// <summary>The element's style, or an all-defaults style for anonymous text items.</summary>
        public required ComputedStyle Style { get; init; }

        /// <summary>Resolved margins; auto counts as zero and is flagged in <see cref="AutoMargin"/>.</summary>
        public float[] Margin { get; } = new float[4];
        public bool[] AutoMargin { get; } = new bool[4];
    }

    List<LayoutItem> CollectItems(ElementNode container, float containingWidth)
    {
        var items = new List<LayoutItem>();
        foreach (var child in container.PhysicalChildren)
        {
            if (child is TextNode text)
            {
                text.Runs.Clear();
                if (string.IsNullOrWhiteSpace(text.Text)) continue;
                items.Add(new LayoutItem { Container = container, Text = text, Style = DefaultItemStyle });
            }
            else if (child is ElementNode element && element.Style.Display != Display.None && !element.Style.IsOutOfFlow)
            {
                var item = new LayoutItem { Container = container, El = element, Style = element.Style };
                for (int side = 0; side < 4; side++)
                {
                    var margin = element.Style.Margin[side];
                    item.AutoMargin[side] = margin.IsAuto;
                    item.Margin[side] = margin.Resolve(containingWidth) ?? 0;
                }
                items.Add(item);
            }
        }
        return items.OrderBy(i => i.Style.Order).ToList(); // LINQ ordering is stable
    }

    /// <summary>Border-box width at max-content or min-content.</summary>
    float IntrinsicWidth(LayoutItem item, bool min)
    {
        if (item.El is not { } el) return TextIntrinsicWidth(item.Text!, min);

        // A scroll container's automatic minimum size is zero, so it can shrink below its content.
        if (min && el.Style.OverflowX != Overflow.Visible && el.Style.Width.IsAuto)
            return HorizontalChrome(el.Style);

        return IntrinsicBorderWidth(el, min);
    }

    float OuterIntrinsicWidth(LayoutItem item, bool min) => IntrinsicWidth(item, min) + item.Margin[L] + item.Margin[R];

    static float TextIntrinsicWidth(TextNode text, bool min)
    {
        var style = text.ParentElement!.Style;
        bool previousWasSpace = true;
        float widest = 0, run = 0;
        foreach (var piece in SplitCollapsed(text.Text, ref previousWasSpace))
        {
            if (!min) { run += FontCache.Measure(piece, style); continue; }
            if (piece != " ") widest = Math.Max(widest, FontCache.Measure(piece, style));
        }
        return min ? widest : run;
    }

    /// <summary>Border-box height the item takes when laid out at the given border-box width.</summary>
    float NaturalHeight(LayoutItem item, float borderWidth, float containingWidth)
    {
        if (item.El is { } el)
        {
            if (_heightCache.TryGetValue((el, borderWidth), out var cached)) return cached;
            LayoutBlock(el, 0, 0, containingWidth, shrink: false, forcedWidth: borderWidth);
            return _heightCache[(el, borderWidth)] = el.BorderRect.Height;
        }

        return LayoutInline(item.Container, new Node[] { item.Text! }, 0, 1, 0, 0, borderWidth);
    }

    /// <summary>Positions an item's border box at (x, y), optionally forcing its border-box size.</summary>
    void PlaceItem(LayoutItem item, float x, float y, float? width, float? height, float containingWidth)
    {
        if (item.El is { } el)
        {
            LayoutBlock(el, x - item.Margin[L], y - item.Margin[T], containingWidth,
                shrink: width is null, forcedWidth: width, forcedHeight: height);
        }
        else
        {
            LayoutInline(item.Container, new Node[] { item.Text! }, 0, 1, x, y,
                width ?? TextIntrinsicWidth(item.Text!, min: false));
        }
    }

    // ---- style queries ----

    static float HorizontalChromeAt(ComputedStyle st, float containingWidth) =>
        (st.Padding[L].Resolve(containingWidth) ?? 0) + (st.Padding[R].Resolve(containingWidth) ?? 0)
        + st.BorderWidth[L] + st.BorderWidth[R];

    /// <summary>The explicit width as a border-box size, or null when auto.</summary>
    static float? SpecifiedBorderWidth(LayoutItem item, float containingWidth)
    {
        var st = item.Style;
        if (st.Width.Resolve(containingWidth) is not { } w) return null;
        float chrome = HorizontalChromeAt(st, containingWidth);
        return st.BorderBox ? Math.Max(w, chrome) : w + chrome;
    }

    /// <summary>The explicit height as a border-box size; percentages need a definite container height.</summary>
    static float? SpecifiedBorderHeight(LayoutItem item, float containingWidth, float? containingHeight)
    {
        var st = item.Style;
        float? h = st.Height.Unit == LengthUnit.Percent
            ? containingHeight is { } ch ? st.Height.Value / 100f * ch : null
            : st.Height.Resolve(0);
        if (h is not { } height) return null;
        float chrome = VerticalChrome(st, containingWidth);
        return st.BorderBox ? Math.Max(height, chrome) : height + chrome;
    }

    /// <summary>Applies min/max-width (or height) to a border-box size.</summary>
    static float ClampBorderSize(float border, float chrome, Length min, Length max, float containing, bool borderBox)
    {
        float content = Math.Max(0, border - chrome);
        content = Clamp(content, min, max, containing, borderBox ? chrome : 0);
        return content + chrome;
    }

    /// <summary>How free space is split for justify-content and align-content: (offset before the first item, extra gap between items).</summary>
    static (float start, float between) Distribute(float free, int count, Align mode)
    {
        if (free < 0)
        {
            // Not enough room: distributed modes fall back as the specs describe.
            mode = mode switch { Align.SpaceBetween => Align.Start, Align.SpaceAround or Align.SpaceEvenly => Align.Center, _ => mode };
        }

        return mode switch
        {
            Align.End => (free, 0),
            Align.Center => (free / 2, 0),
            Align.SpaceBetween => count > 1 ? (0, free / (count - 1)) : (0, 0),
            Align.SpaceAround => (free / count / 2, free / count),
            Align.SpaceEvenly => (free / (count + 1), free / (count + 1)),
            _ => (0, 0)
        };
    }
}

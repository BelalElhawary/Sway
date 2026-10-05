using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Lays out children in runs, wrapping to a new run when the main axis is full.</summary>
public sealed class Wrap(IReadOnlyList<Widget> children, Axis direction = Axis.Horizontal, WrapAlignment alignment = WrapAlignment.Start,
    float spacing = 0, WrapAlignment runAlignment = WrapAlignment.Start, float runSpacing = 0,
    WrapCrossAlignment crossAxisAlignment = WrapCrossAlignment.Start, TextDirection? textDirection = null,
    VerticalDirection verticalDirection = VerticalDirection.Down, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderWrap(direction, alignment, spacing, runAlignment, runSpacing,
        crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderWrap)ro).Update(direction, alignment, spacing, runAlignment,
        runSpacing, crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection);
}

/// <summary>
/// CSS-grid-style container. <c>new Grid(children, columns: [GridTrack.Fr(1), GridTrack.Px(200)], rowGap: 8)</c>.
/// Wrap children in <see cref="GridItem"/> for explicit placement, spans or alignment; others flow row by row.
/// </summary>
public sealed class Grid(IReadOnlyList<Widget> children, IReadOnlyList<GridTrack>? columns = null, IReadOnlyList<GridTrack>? rows = null,
    GridTrack? autoRows = null, float columnGap = 0, float rowGap = 0, float autoFillMinColumnWidth = 0, Key? key = null)
    : MultiChildRenderObjectWidget(children, key)
{
    /// <summary>A grid of <paramref name="count"/> equal columns.</summary>
    public static Grid Count(int count, IReadOnlyList<Widget> children, float gap = 0, float? rowGap = null, GridTrack? autoRows = null) =>
        new(children, Enumerable.Repeat(GridTrack.Fr(1), count).ToList(), null, autoRows, gap, rowGap ?? gap);

    /// <summary>As many columns as fit, each at least <paramref name="minColumnWidth"/> wide and sharing the leftover space (CSS auto-fill + minmax).</summary>
    public static Grid AutoFill(float minColumnWidth, IReadOnlyList<Widget> children, float gap = 0, float? rowGap = null, GridTrack? autoRows = null) =>
        new(children, null, null, autoRows, gap, rowGap ?? gap, minColumnWidth);

    IReadOnlyList<GridTrack> Cols => columns ?? Array.Empty<GridTrack>();
    IReadOnlyList<GridTrack> Rows => rows ?? Array.Empty<GridTrack>();

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderGrid(Cols, Rows, autoRows ?? GridTrack.Auto, columnGap, rowGap, autoFillMinColumnWidth);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderGrid)ro).Update(Cols, Rows, autoRows ?? GridTrack.Auto, columnGap, rowGap, autoFillMinColumnWidth);
}

/// <summary>Places a child in a <see cref="Grid"/>: zero-based column/row, spans, and optional alignment inside the cell (null stretches).</summary>
public sealed class GridItem(Widget child, int? column = null, int? row = null, int columnSpan = 1, int rowSpan = 1,
    IAlignment? alignment = null, Key? key = null) : ParentDataWidget(child, key)
{
    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderGrid;

    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not GridParentData pd) ro.ParentData = pd = new GridParentData();
        var align = alignment?.Resolve(TextDirection.Ltr);
        if (pd.Column == column && pd.Row == row && pd.ColumnSpan == columnSpan && pd.RowSpan == rowSpan && pd.Alignment == align) return;
        pd.Column = column; pd.Row = row; pd.ColumnSpan = columnSpan; pd.RowSpan = rowSpan; pd.Alignment = align;
        ro.Parent?.MarkNeedsLayout();
    }
}

/// <summary>Blurs and/or recolours what is behind it (the equivalent of CSS <c>backdrop-filter</c>), within its own bounds.</summary>
public sealed class BackdropFilter(Widget? child = null, float blur = 0, SKColorFilter? colorFilter = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderBackdropFilter(blur, colorFilter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderBackdropFilter)ro).Update(blur, colorFilter);
}

/// <summary>Colour filters equivalent to the CSS filter functions (grayscale, sepia, saturate, brightness, contrast, hue-rotate, invert).</summary>
public static class ColorFilters
{
    static SKColorFilter M(params float[] m) => SKColorFilter.CreateColorMatrix(m);
    static float L(float a, float b, float t) => a + (b - a) * t;

    public static SKColorFilter Grayscale(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1);
        float r = L(1, 0.2126f, t), g = L(0, 0.7152f, t), b = L(0, 0.0722f, t);
        return M(L(1, 0.2126f, t), L(0, 0.7152f, t), L(0, 0.0722f, t), 0, 0,
                 L(0, 0.2126f, t), L(1, 0.7152f, t), L(0, 0.0722f, t), 0, 0,
                 L(0, 0.2126f, t), L(0, 0.7152f, t), L(1, 0.0722f, t), 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Sepia(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1);
        return M(L(1, 0.393f, t), L(0, 0.769f, t), L(0, 0.189f, t), 0, 0,
                 L(0, 0.349f, t), L(1, 0.686f, t), L(0, 0.168f, t), 0, 0,
                 L(0, 0.272f, t), L(0, 0.534f, t), L(1, 0.131f, t), 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Saturate(float amount)
    {
        float s = amount;
        return M(0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s, 0, 0,
                 0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s, 0, 0,
                 0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s, 0, 0,
                 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Brightness(float amount) => M(amount, 0, 0, 0, 0, 0, amount, 0, 0, 0, 0, 0, amount, 0, 0, 0, 0, 0, 1, 0);

    public static SKColorFilter Contrast(float amount)
    {
        float o = 0.5f * (1 - amount);
        return M(amount, 0, 0, 0, o, 0, amount, 0, 0, o, 0, 0, amount, 0, o, 0, 0, 0, 1, 0);
    }

    public static SKColorFilter Invert(float amount = 1)
    {
        float t = Math.Clamp(amount, 0, 1), s = 1 - 2 * t;
        float o = t;
        return M(s, 0, 0, 0, o, 0, s, 0, 0, o, 0, 0, s, 0, o, 0, 0, 0, 1, 0);
    }

    public static SKColorFilter HueRotate(float degrees)
    {
        float a = degrees * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
        return M(0.213f + c * 0.787f - s * 0.213f, 0.715f - c * 0.715f - s * 0.715f, 0.072f - c * 0.072f + s * 0.928f, 0, 0,
                 0.213f - c * 0.213f + s * 0.143f, 0.715f + c * 0.285f + s * 0.140f, 0.072f - c * 0.072f - s * 0.283f, 0, 0,
                 0.213f - c * 0.213f - s * 0.787f, 0.715f - c * 0.715f + s * 0.715f, 0.072f + c * 0.928f + s * 0.072f, 0, 0,
                 0, 0, 0, 1, 0);
    }
}

/// <summary>Blurs and/or recolours its child (the equivalent of CSS <c>filter</c>).</summary>
public sealed class ImageFiltered(Widget? child = null, float blur = 0, SKColorFilter? colorFilter = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderImageFilter(blur, colorFilter);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderImageFilter)ro).Update(blur, colorFilter);
}

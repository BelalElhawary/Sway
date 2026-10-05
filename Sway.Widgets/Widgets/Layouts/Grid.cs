using SkiaSharp;

namespace Sway.Widgets;

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

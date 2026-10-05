using SkiaSharp;

namespace Sway.Widgets;

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

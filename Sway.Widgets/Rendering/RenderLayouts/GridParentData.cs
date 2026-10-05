using SkiaSharp;

namespace Sway.Widgets;

public sealed class GridParentData : BoxParentData
{
    public int? Column, Row;
    public int ColumnSpan = 1, RowSpan = 1;
    public Alignment? Alignment;
    internal int R, C;
}

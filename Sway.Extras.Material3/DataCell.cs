using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>One cell of a <see cref="DataRow"/>.</summary>
public sealed record DataCell(Widget Child)
{
    /// <summary>A cell showing plain text.</summary>
    public DataCell(string text) : this(new Text(text)) { }
}

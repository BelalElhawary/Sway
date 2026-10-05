using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A column of a Material <see cref="DataTable"/>. Numeric columns align their cells to the end.</summary>
/// <param name="Width">The column width in logical pixels.</param>
/// <param name="OnSort">Makes the header sortable; receives the column index and the direction being asked for.</param>
public sealed record DataColumn(Widget Label, bool Numeric = false, float Width = 160, Action<int, bool>? OnSort = null);

namespace Sway.Extras.Material3;

/// <summary>A row of a <see cref="DataTable"/>, one cell per column. A row with <paramref name="OnSelectChanged"/> shows a checkbox.</summary>
public sealed record DataRow(IReadOnlyList<DataCell> Cells, bool Selected = false, Action<bool>? OnSelectChanged = null, Action? OnTap = null);

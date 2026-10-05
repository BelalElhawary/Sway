using Sway.Widgets;

namespace Sway.Extras.Material3;

public static class DateRangePicker
{
    /// <summary>
    /// Shows a Material 3 date-range picker dialog. <paramref name="onSelected"/> runs with the chosen range when Save is pressed,
    /// which needs both ends picked.
    /// </summary>
    public static Action Show(BuildContext context, DateTime firstDate, DateTime lastDate, Action<DateRange> onSelected, DateRange? initialRange = null) =>
        Dialogs.Show(context, (_, close) => new DateRangePickerDialog(firstDate.Date, lastDate.Date, initialRange, close, r => { close(); onSelected(r); }));
}

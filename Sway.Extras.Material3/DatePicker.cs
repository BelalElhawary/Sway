using Sway.Widgets;

namespace Sway.Extras.Material3;

public static class DatePicker
{
    /// <summary>Shows a Material 3 date picker dialog. <paramref name="onSelected"/> runs with the chosen date when OK is pressed.</summary>
    public static Action Show(BuildContext context, DateTime initialDate, DateTime firstDate, DateTime lastDate, Action<DateTime> onSelected) =>
        Dialogs.Show(context, (_, close) => new DatePickerDialog(initialDate.Date, firstDate.Date, lastDate.Date, close, d => { close(); onSelected(d); }));
}

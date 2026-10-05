using Sway.Widgets;

namespace Sway.Extras.Material3;

public static class TimePicker
{
    /// <summary>Shows a Material 3 time picker dialog. <paramref name="onSelected"/> runs with the chosen time when OK is pressed.</summary>
    public static Action Show(BuildContext context, TimeOfDay initialTime, Action<TimeOfDay> onSelected) =>
        Dialogs.Show(context, (_, close) => new TimePickerDialog(initialTime, close, t => { close(); onSelected(t); }));
}

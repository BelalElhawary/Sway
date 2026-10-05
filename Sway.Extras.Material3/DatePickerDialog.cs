using System.Globalization;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class DatePickerDialog(DateTime initialDate, DateTime firstDate, DateTime lastDate, Action onCancel, Action<DateTime> onConfirm,
    Key? key = null) : StatefulWidget(key)
{
    internal DateTime Initial => initialDate;
    internal DateTime First => firstDate;
    internal DateTime Last => lastDate;
    internal Action OnCancel => onCancel;
    internal Action<DateTime> OnConfirm => onConfirm;
    public override State CreateState() => new DatePickerDialogState();
}

sealed class DatePickerDialogState : State<DatePickerDialog>
{
    const float Cell = 40;

    DateTime _selected, _month;

    public override void InitState()
    {
        _selected = Clamp(Widget.Initial);
        _month = new DateTime(_selected.Year, _selected.Month, 1);
    }

    DateTime Clamp(DateTime d) => d < Widget.First ? Widget.First : d > Widget.Last ? Widget.Last : d;

    bool CanGoBack => new DateTime(_month.Year, _month.Month, 1) > new DateTime(Widget.First.Year, Widget.First.Month, 1);
    bool CanGoForward => new DateTime(_month.Year, _month.Month, 1) < new DateTime(Widget.Last.Year, Widget.Last.Month, 1);

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var l10n = MaterialLocalizations.Of(context);
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        var today = DateTime.Today;
        int daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        int leading = ((int)_month.DayOfWeek - (int)l10n.FirstDayOfWeek + 7) % 7;

        var weekdays = Enumerable.Range(0, 7).Select(i => l10n.NarrowWeekday((DayOfWeek)(((int)l10n.FirstDayOfWeek + i) % 7)))
            .Select(d => (Widget)new SizedBox(Cell, Cell, new Center(new Text(d, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurface)))))).ToList();

        var weeks = new List<Widget>();
        for (int start = 1 - leading; start <= daysInMonth; start += 7)
        {
            var days = new List<Widget>();
            for (int day = start; day < start + 7; day++)
            {
                if (day < 1 || day > daysInMonth)
                {
                    days.Add(new SizedBox(Cell, Cell));
                    continue;
                }
                var date = new DateTime(_month.Year, _month.Month, day);
                bool selected = date == _selected, isToday = date == today, enabled = date >= Widget.First && date <= Widget.Last;
                string label = l10n.FormatDayNumber(day); // the builder below runs later, after the loop variable has moved on
                days.Add(new Interactive((ctx, st) =>
                {
                    var fg = !enabled ? s.OnSurface.WithOpacity(0.38f) : selected ? s.OnPrimary : isToday ? s.Primary : s.OnSurface;
                    return new AnimatedContainer(TimeSpan.FromMilliseconds(120), width: Cell, height: Cell, alignment: Alignment.Center,
                        decoration: new BoxDecoration(
                            Color: selected ? s.Primary : StateLayer.Blend(Colors.Transparent, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0),
                            Shape: BoxShape.Circle, Border: isToday && !selected ? Border.All(s.Outline) : null),
                        child: new Text(label, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: fg))));
                }, enabled ? () => SetState(() => _selected = date) : null));
            }
            weeks.Add(new Row(days));
        }

        return new SizedBox(width: 328, child: new Material(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            new Padding(EdgeInsets.Only(left: 24, right: 12, top: 16, bottom: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 36, children:
            [
                new Text(l10n.SelectDateLabel, style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                new Text(l10n.FormatMediumDate(_selected), style: theme.TextTheme.HeadlineMedium.Merge(new TextStyle(Color: s.OnSurface))),
            ])),
            new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)),
            new Padding(EdgeInsets.Symmetric(horizontal: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, children:
            [
                new SizedBox(height: 48, child: new Row(children:
                [
                    new Expanded(new Padding(EdgeInsets.Only(left: 12), new Text(l10n.FormatMonthYear(_month),
                        style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))))),
                    new IconButton(new Icon(rtl ? Icons.ChevronRight : Icons.ChevronLeft), CanGoBack ? () => SetState(() => _month = _month.AddMonths(-1)) : null),
                    new IconButton(new Icon(rtl ? Icons.ChevronLeft : Icons.ChevronRight), CanGoForward ? () => SetState(() => _month = _month.AddMonths(1)) : null),
                ])),
                new Row(weekdays),
                ..weeks,
            ])),
            new Padding(EdgeInsets.Only(left: 12, right: 12, top: 8, bottom: 8), new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new TextButton(new Text(l10n.CancelButtonLabel), Widget.OnCancel),
                new TextButton(new Text(l10n.OkButtonLabel), () => Widget.OnConfirm(_selected)),
            ]))),
        ]), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}

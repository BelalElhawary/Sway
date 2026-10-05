using System.Globalization;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The Material 3 date-range picker: tap a start day, then an end day. Tapping again after both are set starts a new range.</summary>
public sealed class DateRangePickerDialog(DateTime firstDate, DateTime lastDate, DateRange? initialRange, Action onCancel, Action<DateRange> onConfirm,
    Key? key = null) : StatefulWidget(key)
{
    internal DateTime First => firstDate;
    internal DateTime Last => lastDate;
    internal DateRange? Initial => initialRange;
    internal Action OnCancel => onCancel;
    internal Action<DateRange> OnConfirm => onConfirm;
    public override State CreateState() => new DateRangePickerDialogState();
}

sealed class DateRangePickerDialogState : State<DateRangePickerDialog>
{
    static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    const float Cell = 40;

    DateTime? _start, _end;
    DateTime _month;

    public override void InitState()
    {
        _start = Widget.Initial?.Start.Date;
        _end = Widget.Initial?.End.Date;
        var anchor = _start ?? DateTime.Today;
        if (anchor < Widget.First) anchor = Widget.First;
        if (anchor > Widget.Last) anchor = Widget.Last;
        _month = new DateTime(anchor.Year, anchor.Month, 1);
    }

    bool CanGoBack => _month > new DateTime(Widget.First.Year, Widget.First.Month, 1);
    bool CanGoForward => _month < new DateTime(Widget.Last.Year, Widget.Last.Month, 1);

    void Pick(DateTime date) => SetState(() =>
    {
        if (_start is null || _end is not null || date < _start) { _start = date; _end = null; }
        else _end = date;
    });

    static string Format(DateTime? d) => d?.ToString("MMM d", Culture) ?? "Start date";

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var today = DateTime.Today;
        int daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        int leading = (int)_month.DayOfWeek; // weeks start on Sunday

        var weekdays = new[] { "S", "M", "T", "W", "T", "F", "S" }
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
                bool enabled = date >= Widget.First && date <= Widget.Last;
                bool isEnd = _end == date, isStart = _start == date;
                bool endpoint = isStart || isEnd;
                bool inside = _start is not null && _end is not null && date > _start && date < _end;
                bool isToday = date == today;
                string label = day.ToString(Culture);
                // The band behind the days is a separate full-width strip, so the endpoints get a half band on their inner side.
                bool bandLeft = inside || isEnd && _start != _end, bandRight = inside || isStart && _end is not null && _start != _end;
                days.Add(new Interactive((ctx, st) =>
                {
                    var fg = !enabled ? s.OnSurface.WithOpacity(0.38f) : endpoint ? s.OnPrimary : inside ? s.OnPrimaryContainer : isToday ? s.Primary : s.OnSurface;
                    return new SizedBox(Cell, Cell, new Stack([
                        ..bandLeft || bandRight ? [new Positioned(new ColoredBox(s.PrimaryContainer),
                            left: bandLeft ? 0 : Cell / 2, right: bandRight ? 0 : Cell / 2, top: 2, bottom: 2)] : Array.Empty<Widget>(),
                        new Container(width: Cell, height: Cell, alignment: Alignment.Center, decoration: new BoxDecoration(
                            Color: endpoint ? s.Primary : StateLayer.Blend(Colors.Transparent, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0),
                            Shape: BoxShape.Circle, Border: isToday && !endpoint ? Border.All(s.Outline) : null),
                            child: new Text(label, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: fg)))),
                    ], clip: false));
                }, enabled ? () => Pick(date) : null));
            }
            weeks.Add(new Row(days));
        }

        return new SizedBox(width: 328, child: new Material(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            new Padding(EdgeInsets.Only(left: 24, right: 12, top: 16, bottom: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, spacing: 36, children:
            [
                new Text("Select range", style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                new Text($"{Format(_start)} – {(_end is null ? "End date" : Format(_end))}",
                    style: theme.TextTheme.HeadlineSmall.Merge(new TextStyle(Color: s.OnSurface))),
            ])),
            new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant)),
            new Padding(EdgeInsets.Symmetric(horizontal: 12), new Column(crossAxisAlignment: CrossAxisAlignment.Start, children:
            [
                new SizedBox(height: 48, child: new Row(children:
                [
                    new Expanded(new Padding(EdgeInsets.Only(left: 12), new Text(_month.ToString("MMMM yyyy", Culture),
                        style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))))),
                    new IconButton(new Icon(Icons.ChevronLeft), CanGoBack ? () => SetState(() => _month = _month.AddMonths(-1)) : null),
                    new IconButton(new Icon(Icons.ChevronRight), CanGoForward ? () => SetState(() => _month = _month.AddMonths(1)) : null),
                ])),
                new Row(weekdays),
                ..weeks,
            ])),
            new Padding(EdgeInsets.Only(left: 12, right: 12, top: 8, bottom: 8), new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new TextButton(new Text("Cancel"), Widget.OnCancel),
                new TextButton(new Text("Save"), _start is { } a && _end is { } b ? () => Widget.OnConfirm(new DateRange(a, b)) : null),
            ]))),
        ]), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}

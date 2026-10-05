using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The Material 3 dial time picker: choose the hour on the dial, then the minute.</summary>
public sealed class TimePickerDialog(TimeOfDay initialTime, Action onCancel, Action<TimeOfDay> onConfirm, Key? key = null) : StatefulWidget(key)
{
    internal TimeOfDay Initial => initialTime;
    internal Action OnCancel => onCancel;
    internal Action<TimeOfDay> OnConfirm => onConfirm;
    public override State CreateState() => new TimePickerDialogState();
}

sealed class TimePickerDialogState : State<TimePickerDialog>
{
    const float DialSize = 256, LabelRadius = 100;

    int _hour, _minute;
    bool _pickingMinute;

    public override void InitState()
    {
        _hour = Math.Clamp(Widget.Initial.Hour, 0, 23);
        _minute = Math.Clamp(Widget.Initial.Minute, 0, 59);
    }

    bool IsPm => _hour >= 12;

    void SetPeriod(bool pm) => SetState(() => _hour = _hour % 12 + (pm ? 12 : 0));

    void FromPointer(Offset local)
    {
        float dx = local.Dx - DialSize / 2, dy = local.Dy - DialSize / 2;
        if (dx == 0 && dy == 0) return;
        float degrees = MathF.Atan2(dx, -dy) * 180 / MathF.PI;
        if (degrees < 0) degrees += 360;
        SetState(() =>
        {
            if (_pickingMinute) _minute = (int)MathF.Round(degrees / 6) % 60;
            else _hour = (int)MathF.Round(degrees / 30) % 12 + (IsPm ? 12 : 0);
        });
    }

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;

        Widget Box(string text, bool selected, Action onTap) => new Interactive((ctx, st) => new AnimatedContainer(TimeSpan.FromMilliseconds(120),
            width: 96, height: 80, alignment: Alignment.Center,
            decoration: new BoxDecoration(
                Color: StateLayer.Blend(selected ? s.PrimaryContainer : s.SurfaceContainerHighest, selected ? s.OnPrimaryContainer : s.OnSurface, StateLayer.Opacity(st)),
                BorderRadius: BorderRadius.Circular(Shapes.Small)),
            child: new Text(text, style: theme.TextTheme.DisplayLarge.Merge(new TextStyle(
                Color: selected ? s.OnPrimaryContainer : s.OnSurface, FontSize: 52)))), onTap);

        Widget Period(string text, bool selected, Action onTap, BorderRadius radius) => new Interactive((ctx, st) => new Container(
            width: 52, height: 40, alignment: Alignment.Center,
            decoration: new BoxDecoration(
                Color: StateLayer.Blend(selected ? s.TertiaryContainer : Colors.Transparent, selected ? s.OnTertiaryContainer : s.OnSurfaceVariant, StateLayer.Opacity(st)),
                BorderRadius: radius, Border: Border.All(s.Outline)),
            child: new Text(text, style: theme.TextTheme.TitleMedium.Merge(new TextStyle(Color: selected ? s.OnTertiaryContainer : s.OnSurfaceVariant)))), onTap);

        // The dial labels: 1-12 for hours, 0-55 in fives for minutes.
        var labels = new List<Widget>();
        for (int i = 0; i < 12; i++)
        {
            int value = _pickingMinute ? i * 5 : i == 0 ? 12 : i;
            bool selected = _pickingMinute ? _minute == value : _hour % 12 == i;
            float rad = (i * 30 - 90) * MathF.PI / 180;
            labels.Add(new Positioned(new IgnorePointer(new Center(new Text(value.ToString(),
                style: theme.TextTheme.BodyLarge.Merge(new TextStyle(Color: selected ? s.OnPrimary : s.OnSurface))))),
                left: DialSize / 2 + MathF.Cos(rad) * LabelRadius - 20, top: DialSize / 2 + MathF.Sin(rad) * LabelRadius - 20, width: 40, height: 40));
        }

        float hand = _pickingMinute ? _minute * 6 : _hour % 12 * 30;
        Widget dial = new GestureDetector(
            behavior: HitTestBehavior.Opaque,
            onTapDown: d => FromPointer(d.LocalPosition),
            onTapUp: _ => { if (!_pickingMinute) SetState(() => _pickingMinute = true); },
            onPanUpdate: d => FromPointer(d.LocalPosition),
            onPanEnd: _ => { if (!_pickingMinute) SetState(() => _pickingMinute = true); },
            child: new SizedBox(DialSize, DialSize, new Stack([
                Positioned.Fill(new CustomPaint(new DialPainter(hand, s.SurfaceContainerHighest, s.Primary, LabelRadius,
                    _pickingMinute && _minute % 5 != 0))),
                ..labels,
            ], clip: false)));

        return new SizedBox(width: 328, child: new Material(new Padding(EdgeInsets.All(24), new Column(
            mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, spacing: 20, children:
            [
                new Text("Select time", style: theme.TextTheme.LabelLarge.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                new Row(mainAxisAlignment: MainAxisAlignment.Center, spacing: 12, children:
                [
                    Box(new TimeOfDay(_hour, 0).HourOfPeriod.ToString("00"), !_pickingMinute, () => SetState(() => _pickingMinute = false)),
                    new Text(":", style: theme.TextTheme.DisplayLarge.Merge(new TextStyle(Color: s.OnSurface, FontSize: 52))),
                    Box(_minute.ToString("00"), _pickingMinute, () => SetState(() => _pickingMinute = true)),
                    new Column(mainAxisSize: MainAxisSize.Min, children:
                    [
                        Period("AM", !IsPm, () => SetPeriod(false), new BorderRadius(new Radius(8, 8), new Radius(8, 8), new Radius(0, 0), new Radius(0, 0))),
                        Period("PM", IsPm, () => SetPeriod(true), new BorderRadius(new Radius(0, 0), new Radius(0, 0), new Radius(8, 8), new Radius(8, 8))),
                    ]),
                ]),
                new Center(dial),
                new Align(Alignment.CenterRight, new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    new TextButton(new Text("Cancel"), Widget.OnCancel),
                    new TextButton(new Text("OK"), () => Widget.OnConfirm(new TimeOfDay(_hour, _minute))),
                ])),
            ])), s.SurfaceContainerHigh, 3, BorderRadius.Circular(Shapes.ExtraLarge)));
    }
}

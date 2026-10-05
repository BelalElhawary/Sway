namespace Sway.Extras.Material3;

/// <summary>A time on a 24-hour clock, with no date.</summary>
public readonly record struct TimeOfDay(int Hour, int Minute)
{
    public static TimeOfDay Now => new(DateTime.Now.Hour, DateTime.Now.Minute);

    public bool IsPm => Hour >= 12;

    /// <summary>The hour as read on a 12-hour clock, 1 to 12.</summary>
    public int HourOfPeriod => Hour % 12 == 0 ? 12 : Hour % 12;

    public override string ToString() => $"{HourOfPeriod}:{Minute:00} {(IsPm ? "PM" : "AM")}";
}

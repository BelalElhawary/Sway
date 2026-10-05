namespace Sway.Extras.Material3;

/// <summary>An inclusive span of dates; <see cref="Start"/> is never after <see cref="End"/>.</summary>
public readonly record struct DateRange(DateTime Start, DateTime End)
{
    public bool Contains(DateTime date) => date.Date >= Start.Date && date.Date <= End.Date;
}

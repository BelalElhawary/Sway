using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class TimePickerTests
{
    static (Harness harness, Func<TimeOfDay?> chosen) Show(TimeOfDay initial)
    {
        TimeOfDay? chosen = null;
        var (h, ctx) = App(new SizedBox(), height: 700);
        TimePicker.Show(ctx(), initial, t => chosen = t);
        Open(h);
        return (h, () => chosen);
    }

    [Fact]
    public void TimeOfDayReadsOnATwelveHourClock()
    {
        Assert.Equal(12, new TimeOfDay(0, 5).HourOfPeriod);
        Assert.Equal(12, new TimeOfDay(12, 5).HourOfPeriod);
        Assert.Equal(3, new TimeOfDay(15, 5).HourOfPeriod);
        Assert.Equal("3:05 PM", new TimeOfDay(15, 5).ToString());
    }

    [Fact]
    public void ShowsTheInitialTime()
    {
        var (h, _) = Show(new TimeOfDay(15, 30));
        var texts = Texts(h);
        Assert.Contains("03", texts);
        Assert.Contains("30", texts);
        Assert.Contains("PM", texts);
        Assert.Contains("12", texts); // dial label
    }

    [Fact]
    public void PickingAnHourOnTheDialMovesOnToMinutes()
    {
        var (h, chosen) = Show(new TimeOfDay(15, 30));
        TapText(h, "9");
        Assert.Contains("45", Texts(h)); // the dial now reads minutes
        TapText(h, "45");
        TapText(h, "OK");
        Assert.Equal(new TimeOfDay(21, 45), chosen());
    }

    [Fact]
    public void AmPmToggleKeepsTheHourOfPeriod()
    {
        var (h, chosen) = Show(new TimeOfDay(15, 30));
        TapText(h, "AM");
        TapText(h, "OK");
        Assert.Equal(new TimeOfDay(3, 30), chosen());
    }

    [Fact]
    public void CancelReturnsNothing()
    {
        var (h, chosen) = Show(new TimeOfDay(9, 0));
        TapText(h, "Cancel");
        h.Advance(300);
        Assert.Null(chosen());
        Assert.DoesNotContain("Select time", Texts(h));
    }
}

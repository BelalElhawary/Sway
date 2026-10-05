using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class DateRangePickerTests
{
    static readonly DateTime First = new(2020, 1, 1), Last = new(2030, 12, 31);

    static (Harness harness, Func<DateRange?> chosen) Show(DateRange? initial = null)
    {
        DateRange? chosen = null;
        var (h, ctx) = App(new SizedBox(), height: 700);
        DateRangePicker.Show(ctx(), First, Last, r => chosen = r, initial);
        Open(h);
        return (h, () => chosen);
    }

    [Fact]
    public void ShowsTheInitialRangeAndItsMonth()
    {
        var (h, _) = Show(new DateRange(new DateTime(2026, 3, 5), new DateTime(2026, 3, 9)));
        var texts = Texts(h);
        Assert.Contains("Mar 5 – Mar 9", texts);
        Assert.Contains("March 2026", texts);
    }

    [Fact]
    public void TwoTapsPickARangeAndSaveReturnsIt()
    {
        var (h, chosen) = Show(new DateRange(new DateTime(2026, 3, 5), new DateTime(2026, 3, 9)));
        TapText(h, "12"); // both ends were set, so this starts a new range
        Assert.Contains("Mar 12 – End date", Texts(h));
        TapText(h, "20");
        Assert.Contains("Mar 12 – Mar 20", Texts(h));
        TapText(h, "Save");
        Assert.Equal(new DateRange(new DateTime(2026, 3, 12), new DateTime(2026, 3, 20)), chosen());
        h.Advance(300);
        Assert.DoesNotContain("March 2026", Texts(h));
    }

    [Fact]
    public void AnEndBeforeTheStartRestartsTheRange()
    {
        var (h, _) = Show(new DateRange(new DateTime(2026, 3, 5), new DateTime(2026, 3, 9)));
        TapText(h, "20");
        TapText(h, "15");
        Assert.Contains("Mar 15 – End date", Texts(h));
    }

    [Fact]
    public void SaveDoesNothingUntilBothEndsArePicked()
    {
        var (h, chosen) = Show(new DateRange(new DateTime(2026, 3, 5), new DateTime(2026, 3, 9)));
        TapText(h, "12");
        TapText(h, "Save");
        Assert.Null(chosen());
        Assert.Contains("March 2026", Texts(h));
    }
}

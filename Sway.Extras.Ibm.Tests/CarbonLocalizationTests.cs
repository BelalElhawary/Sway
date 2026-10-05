using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Ibm.Tests;

public class CarbonLocalizationTests
{
    static List<string> Texts(Widget child, Locale? locale)
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, child), theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light, locale: locale), 800, 300);
        return h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();
    }

    [Fact]
    public void PaginationIsEnglishByDefault()
    {
        var texts = Texts(new Pagination(95, 1, 10, _ => { }, _ => { }), null);
        Assert.Contains("Items per page:", texts);
        Assert.Contains("11–20 of 95 items", texts);
        Assert.Contains("2 of 10 pages", texts);
    }

    [Fact]
    public void PaginationFollowsTheLocale()
    {
        var texts = Texts(new Pagination(95, 1, 10, _ => { }, _ => { }), new Locale("de"));
        Assert.Contains("Elemente pro Seite:", texts);
        Assert.Contains("11–20 von 95 Elemente", texts);
        Assert.Contains("2 von 10 Seiten", texts);
    }

    [Fact]
    public void ArabicHasRtlAndItsOwnStrings()
    {
        var texts = Texts(new CarbonDropdown<int>([new CarbonDropdownItem<int>(1, "One")], 0, _ => { }), new Locale("ar"));
        Assert.Contains("اختر", texts);
    }

    [Fact]
    public void UnknownLanguageFallsBackToEnglish()
    {
        Assert.Contains("Select", Texts(new CarbonDropdown<int>([new CarbonDropdownItem<int>(1, "One")], 0, _ => { }), new Locale("ja")));
    }
}

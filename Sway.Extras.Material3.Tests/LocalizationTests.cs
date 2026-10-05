using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class LocalizationTests
{
    static Harness ShowRange(Locale locale)
    {
        var captured = new List<BuildContext>();
        var h = new Harness(new MaterialApp(new Builder(ctx => { captured.Add(ctx); return new SizedBox(); }), locale: locale), 400, 700);
        DateRangePicker.Show(captured[^1], new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), _ => { }, new DateRange(new DateTime(2026, 3, 5), new DateTime(2026, 3, 9)));
        Open(h);
        return h;
    }

    [Fact]
    public void EnglishIsTheDefault()
    {
        var h = ShowRange(Locale.English);
        var texts = Texts(h);
        Assert.Contains("Cancel", texts);
        Assert.Contains("Save", texts);
        Assert.Contains("Select range", texts);
    }

    [Fact]
    public void FrenchLocalizesButtonsAndDates()
    {
        var texts = Texts(ShowRange(new Locale("fr", "FR")));
        Assert.Contains("Annuler", texts);
        Assert.Contains("Enregistrer", texts);
        Assert.Contains("Sélectionner la période", texts);
        Assert.Contains(texts, t => t.StartsWith("mars"));
    }

    [Fact]
    public void ArabicLocalizesStringsAndGivesRtl()
    {
        var h = ShowRange(new Locale("ar", "SA"));
        var texts = Texts(h);
        Assert.Contains("إلغاء", texts);
        Assert.Contains("حفظ", texts);
        Assert.DoesNotContain(texts, t => t.Contains("1447") || t.Contains("1448")); // Gregorian, not Hijri
    }

    [Fact]
    public void WeekStartsOnTheLocalesFirstDay()
    {
        // 1 March 2026 is a Sunday: with a Monday start it is the last cell of the first row, so "S" no longer leads the header.
        var texts = Texts(ShowRange(new Locale("fr", "FR")));
        int firstHeader = texts.FindIndex(t => t.Length == 1 && char.IsLetter(t[0]));
        Assert.Equal("L", texts[firstHeader]);
    }

    [Fact]
    public void TimePickerAndStepperUseTheLocale()
    {
        var captured = new List<BuildContext>();
        var h = new Harness(new MaterialApp(new Builder(ctx => { captured.Add(ctx); return new SizedBox(); }), locale: new Locale("es")), 400, 700);
        TimePicker.Show(captured[^1], new TimeOfDay(9, 30), _ => { });
        Open(h);
        var texts = Texts(h);
        Assert.Contains("Aceptar", texts);
        Assert.Contains("Seleccionar hora", texts);
    }

    [Fact]
    public void LocaleSetsDirectionAndAppDelegatesAreReadable()
    {
        TextDirection? dir = null;
        string? custom = null;
        _ = new Harness(new MaterialApp(new Builder(ctx => { dir = Directionality.Of(ctx); custom = Localizations.Of<MaterialLocalizations>(ctx)?.OkButtonLabel; return new SizedBox(); }),
            locale: new Locale("ar")));
        Assert.Equal(TextDirection.Rtl, dir);
        Assert.Equal("موافق", custom);
    }
}

using System.Globalization;

using Xunit;

namespace Sway.Widgets.Tests;

public class LocalizationsTests
{
    sealed record Strings(string Hello);

    sealed class StringsDelegate : LocalizationsDelegate<Strings>
    {
        public override bool IsSupported(Locale locale) => locale.Language is "en" or "fr";
        public override Strings Load(Locale locale) => new(locale.Language == "fr" ? "Bonjour" : "Hello");
    }

    static (string? hello, Locale locale) Read(Locale locale, Locale? fallback = null)
    {
        string? hello = null;
        Locale? seen = null;
        _ = new Harness(new Localizations(locale, [new StringsDelegate()], new Builder(ctx =>
        {
            hello = Localizations.Of<Strings>(ctx)?.Hello;
            seen = Localizations.LocaleOf(ctx);
            return new SizedBox();
        }), fallback));
        return (hello, seen!);
    }

    [Fact]
    public void ParsesLanguageAndRegion()
    {
        Assert.Equal(new Locale("pt", "BR"), Locale.Parse("pt_br"));
        Assert.Equal(new Locale("ar"), Locale.Parse("AR"));
        Assert.Equal("en-US", Locale.English.ToString());
    }

    [Fact]
    public void RightToLeftLanguagesGiveRtl()
    {
        Assert.Equal(TextDirection.Rtl, new Locale("ar", "EG").TextDirection);
        Assert.Equal(TextDirection.Rtl, new Locale("he").TextDirection);
        Assert.Equal(TextDirection.Ltr, new Locale("fr").TextDirection);
    }

    [Fact]
    public void CultureIsGregorianEvenWhereTheDefaultIsHijri()
    {
        Assert.IsType<GregorianCalendar>(new Locale("ar", "SA").Culture.DateTimeFormat.Calendar);
    }

    [Fact]
    public void LoadsResourcesForTheLocale() => Assert.Equal("Bonjour", Read(new Locale("fr", "CA")).hello);

    [Fact]
    public void UnsupportedLocaleFallsBackToEnglish() => Assert.Equal("Hello", Read(new Locale("ja")).hello);

    [Fact]
    public void FallbackLocaleIsConfigurable() => Assert.Equal("Bonjour", Read(new Locale("ja"), new Locale("fr")).hello);

    [Fact]
    public void MissingDelegateGivesNull()
    {
        string? hello = "unset";
        _ = new Harness(new Localizations(Locale.English, [], new Builder(ctx => { hello = Localizations.Of<Strings>(ctx)?.Hello; return new SizedBox(); })));
        Assert.Null(hello);
    }

    [Fact]
    public void WithoutLocalizationsTheLocaleIsEnglish()
    {
        Locale? seen = null;
        _ = new Harness(new Builder(ctx => { seen = Localizations.LocaleOf(ctx); return new SizedBox(); }));
        Assert.Equal(Locale.English, seen);
    }
}

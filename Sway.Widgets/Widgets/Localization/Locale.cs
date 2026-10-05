using System.Globalization;

namespace Sway.Widgets;

/// <summary>A language and an optional region, such as <c>en</c>, <c>en-US</c> or <c>ar-EG</c>.</summary>
public sealed record Locale(string Language, string? Region = null)
{
    static readonly HashSet<string> RtlLanguages = ["ar", "he", "fa", "ur", "ps", "sd", "ug", "yi", "dv", "ckb"];

    public static Locale English { get; } = new("en", "US");

    /// <summary>The operating system's UI language.</summary>
    public static Locale System => From(CultureInfo.CurrentUICulture);

    /// <summary>Reads <c>language</c> or <c>language-REGION</c> (also with <c>_</c>); the result is lower-case language, upper-case region.</summary>
    public static Locale Parse(string tag)
    {
        var parts = tag.Split('-', '_');
        string? region = parts.Skip(1).FirstOrDefault(p => p.Length == 2);
        return new Locale(parts[0].ToLowerInvariant(), region?.ToUpperInvariant());
    }

    public static Locale From(CultureInfo culture)
    {
        if (culture.TwoLetterISOLanguageName is "iv" or "") return English;
        var parts = culture.Name.Split('-');
        return new Locale(culture.TwoLetterISOLanguageName, parts.Skip(1).FirstOrDefault(p => p.Length == 2)?.ToUpperInvariant());
    }

    public bool IsRightToLeft => RtlLanguages.Contains(Language);
    public TextDirection TextDirection => IsRightToLeft ? TextDirection.Rtl : TextDirection.Ltr;

    /// <summary>
    /// The culture for formatting dates, numbers and weekday names. Always Gregorian (a culture such as <c>ar-SA</c> would otherwise print Hijri dates),
    /// and the invariant culture when the runtime has no data for this locale (invariant-globalization hosts such as WebAssembly).
    /// </summary>
    public CultureInfo Culture
    {
        get
        {
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(ToString()); }
            catch (CultureNotFoundException)
            {
                try { culture = CultureInfo.GetCultureInfo(Language); }
                catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
            }
            if (culture.DateTimeFormat.Calendar is GregorianCalendar) return culture;
            culture = (CultureInfo)culture.Clone();
            culture.DateTimeFormat.Calendar = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() ?? new GregorianCalendar();
            return culture;
        }
    }

    public override string ToString() => Region is null ? Language : $"{Language}-{Region}";
}

using System.Globalization;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>The strings and date formats Material widgets show, for one locale. Built in: English, Arabic, Spanish, French and German.</summary>
public sealed record MaterialLocalizations(
    Locale Locale,
    string OkButtonLabel,
    string CancelButtonLabel,
    string SaveButtonLabel,
    string ContinueButtonLabel,
    string SearchHint,
    string SelectDateLabel,
    string SelectRangeLabel,
    string SelectTimeLabel,
    string StartDateLabel,
    string EndDateLabel,
    string AmLabel,
    string PmLabel,
    string NoMediaLabel,
    string MediaErrorLabel)
{
    public static readonly LocalizationsDelegate<MaterialLocalizations> Delegate = new MaterialLocalizationsDelegate();

    public static MaterialLocalizations English { get; } = Create(Locale.English);

    /// <summary>The nearest resources, or English when the tree has none.</summary>
    public static MaterialLocalizations Of(BuildContext context) => Localizations.Of<MaterialLocalizations>(context) ?? English;

    public static IReadOnlyList<Locale> SupportedLocales { get; } = [new("en"), new("ar"), new("es"), new("fr"), new("de")];

    /// <summary>The built-in strings for <paramref name="locale"/>, in English when its language is not built in.</summary>
    public static MaterialLocalizations Create(Locale locale) => locale.Language switch
    {
        "ar" => new(locale, "موافق", "إلغاء", "حفظ", "متابعة", "بحث", "اختر التاريخ", "اختر النطاق", "اختر الوقت", "تاريخ البداية", "تاريخ النهاية",
            "ص", "م", "لا توجد وسائط", "لا يمكن تشغيل هذه الوسائط"),
        "es" => new(locale, "Aceptar", "Cancelar", "Guardar", "Continuar", "Buscar", "Seleccionar fecha", "Seleccionar intervalo", "Seleccionar hora",
            "Fecha de inicio", "Fecha de fin", "a. m.", "p. m.", "Sin medios", "No se puede reproducir este contenido"),
        "fr" => new(locale, "OK", "Annuler", "Enregistrer", "Continuer", "Rechercher", "Sélectionner la date", "Sélectionner la période", "Sélectionner l'heure",
            "Date de début", "Date de fin", "AM", "PM", "Aucun média", "Impossible de lire ce média"),
        "de" => new(locale, "OK", "Abbrechen", "Speichern", "Weiter", "Suchen", "Datum auswählen", "Zeitraum auswählen", "Uhrzeit auswählen",
            "Startdatum", "Enddatum", "AM", "PM", "Keine Medien", "Dieses Medium kann nicht abgespielt werden"),
        _ => new(locale, "OK", "Cancel", "Save", "Continue", "Search", "Select date", "Select range", "Select time", "Start date", "End date",
            "AM", "PM", "No media", "This media can't be played"),
    };

    public string FinishButtonLabel => Locale.Language switch
    {
        "ar" => "إنهاء", "es" => "Finalizar", "fr" => "Terminer", "de" => "Fertig", _ => "Finish",
    };

    public string BufferingLabel(double percent) => Locale.Language switch
    {
        "ar" => $"جارٍ التحميل المؤقت {percent:0}%", "es" => $"Almacenando {percent:0}%", "fr" => $"Mise en mémoire tampon {percent:0}%",
        "de" => $"Puffern {percent:0}%", _ => $"Buffering {percent:0}%",
    };

    public CultureInfo Culture => Locale.Culture;
    public DayOfWeek FirstDayOfWeek => Culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Header of the date picker, such as "Mon, Jan 5".</summary>
    public string FormatMediumDate(DateTime d) => d.ToString(Locale.Language == "en" ? "ddd, MMM d" : $"ddd, {Culture.DateTimeFormat.MonthDayPattern}", Culture);

    /// <summary>Short date for the range picker, such as "Jan 5".</summary>
    public string FormatShortDate(DateTime d) => d.ToString(Locale.Language == "en" ? "MMM d" : Culture.DateTimeFormat.MonthDayPattern, Culture);

    public string FormatMonthYear(DateTime d) => d.ToString(Locale.Language == "en" ? "MMMM yyyy" : Culture.DateTimeFormat.YearMonthPattern, Culture);

    /// <summary>The one-letter (or shortest) name of a weekday for the calendar header.</summary>
    public string NarrowWeekday(DayOfWeek day)
    {
        var name = Culture.DateTimeFormat.GetShortestDayName(day);
        return name.Length > 1 && Locale.Language is "en" or "de" or "es" or "fr" ? name[..1].ToUpper(Culture) : name;
    }

    public string FormatDayNumber(int day) => day.ToString(CultureInfo.InvariantCulture);
}

sealed class MaterialLocalizationsDelegate : LocalizationsDelegate<MaterialLocalizations>
{
    public override bool IsSupported(Locale locale) => MaterialLocalizations.SupportedLocales.Any(l => l.Language == locale.Language);
    public override MaterialLocalizations Load(Locale locale) => MaterialLocalizations.Create(locale);
}

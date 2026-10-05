using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>The strings Carbon widgets show, for one locale. Built in: English, Arabic, Spanish, French and German.</summary>
public sealed record CarbonLocalizations(
    Locale Locale,
    string Cancel,
    string ChooseAnOption,
    string ChooseOptions,
    string Select,
    string FilterPlaceholder,
    string NoMatchingResults,
    string NoData,
    string TooManyCharacters,
    string EnterValidNumber,
    string UploadFiles,
    string AddFile,
    string ShowMore,
    string ShowLess,
    string ItemsPerPage)
{
    public static readonly LocalizationsDelegate<CarbonLocalizations> Delegate = new CarbonLocalizationsDelegate();

    public static CarbonLocalizations English { get; } = Create(Locale.English);

    public static IReadOnlyList<Locale> SupportedLocales { get; } = [new("en"), new("ar"), new("es"), new("fr"), new("de")];

    /// <summary>The nearest resources, or English when the tree has none.</summary>
    public static CarbonLocalizations Of(BuildContext context) => Localizations.Of<CarbonLocalizations>(context) ?? English;

    /// <summary>The built-in strings for <paramref name="locale"/>, in English when its language is not built in.</summary>
    public static CarbonLocalizations Create(Locale locale) => locale.Language switch
    {
        "ar" => new(locale, "إلغاء", "اختر خيارًا", "اختر خيارات", "اختر", "تصفية...", "لا توجد نتائج مطابقة", "لا توجد بيانات", "عدد الأحرف كبير جدًا",
            "أدخل رقمًا صالحًا", "تحميل الملفات", "إضافة ملف", "عرض المزيد", "عرض أقل", "العناصر في الصفحة:"),
        "es" => new(locale, "Cancelar", "Elija una opción", "Elija opciones", "Seleccionar", "Filtrar...", "No hay resultados coincidentes", "Sin datos",
            "Demasiados caracteres", "Introduzca un número válido", "Cargar archivos", "Añadir archivo", "Mostrar más", "Mostrar menos", "Elementos por página:"),
        "fr" => new(locale, "Annuler", "Choisissez une option", "Choisissez des options", "Sélectionner", "Filtrer...", "Aucun résultat correspondant", "Aucune donnée",
            "Trop de caractères", "Saisissez un nombre valide", "Téléverser des fichiers", "Ajouter un fichier", "Afficher plus", "Afficher moins", "Éléments par page :"),
        "de" => new(locale, "Abbrechen", "Option wählen", "Optionen wählen", "Auswählen", "Filtern...", "Keine passenden Ergebnisse", "Keine Daten",
            "Zu viele Zeichen", "Gültige Zahl eingeben", "Dateien hochladen", "Datei hinzufügen", "Mehr anzeigen", "Weniger anzeigen", "Elemente pro Seite:"),
        _ => new(locale, "Cancel", "Choose an option", "Choose options", "Select", "Filter...", "No matching results", "No data", "Too many characters",
            "Enter a valid number", "Upload files", "Add file", "Show more", "Show less", "Items per page:"),
    };

    // French treats 0 as singular; the other built-in languages only 1. Arabic is written without a plural form here.
    bool One(int n) => Locale.Language == "fr" ? n is 0 or 1 : n == 1;

    /// <summary>The hint under a date field that failed to parse.</summary>
    public string EnterDateAs(string format) => Locale.Language switch
    {
        "ar" => $"أدخل التاريخ بصيغة {format}", "es" => $"Introduzca la fecha como {format}", "fr" => $"Saisissez la date au format {format}",
        "de" => $"Datum im Format {format} eingeben", _ => $"Enter a date as {format}",
    };

    public string ItemsSelected(int n) => Locale.Language switch
    {
        "ar" => $"{n} عنصر محدد",
        "es" => $"{n} {(One(n) ? "elemento seleccionado" : "elementos seleccionados")}",
        "fr" => $"{n} {(One(n) ? "élément sélectionné" : "éléments sélectionnés")}",
        "de" => $"{n} {(One(n) ? "Element" : "Elemente")} ausgewählt",
        _ => $"{n} item{(One(n) ? "" : "s")} selected",
    };

    /// <summary>"1–10 of 40", with the unit ("items") when <paramref name="withUnit"/> and the bar is wide enough.</summary>
    public string ItemRange(int first, int last, int total, bool withUnit)
    {
        var (of, one, many) = Locale.Language switch
        {
            "ar" => ("من", "عنصر", "عنصر"), "es" => ("de", "elemento", "elementos"), "fr" => ("sur", "élément", "éléments"),
            "de" => ("von", "Element", "Elemente"), _ => ("of", "item", "items"),
        };
        return $"{first}–{last} {of} {total}{(withUnit ? " " + (One(total) ? one : many) : "")}";
    }

    public string PageOf(int current, int pages)
    {
        var (of, one, many) = Locale.Language switch
        {
            "ar" => ("من", "صفحة", "صفحة"), "es" => ("de", "página", "páginas"), "fr" => ("sur", "page", "pages"),
            "de" => ("von", "Seite", "Seiten"), _ => ("of", "page", "pages"),
        };
        return $"{current} {of} {pages} {(One(pages) ? one : many)}";
    }
}

sealed class CarbonLocalizationsDelegate : LocalizationsDelegate<CarbonLocalizations>
{
    public override bool IsSupported(Locale locale) => CarbonLocalizations.SupportedLocales.Any(l => l.Language == locale.Language);
    public override CarbonLocalizations Load(Locale locale) => CarbonLocalizations.Create(locale);
}

namespace Sway.Widgets;

/// <summary>Loads one kind of localized resources (a table of strings) for a <see cref="Locale"/>.</summary>
public abstract class LocalizationsDelegate<T> : ILocalizationsDelegate where T : class
{
    public abstract bool IsSupported(Locale locale);

    /// <summary>Builds the resources for <paramref name="locale"/>. Only called for a locale <see cref="IsSupported"/> accepted, or for the fallback.</summary>
    public abstract T Load(Locale locale);

    Type ILocalizationsDelegate.ResourceType => typeof(T);
    object ILocalizationsDelegate.LoadObject(Locale locale) => Load(locale);
}

public interface ILocalizationsDelegate
{
    Type ResourceType { get; }
    bool IsSupported(Locale locale);
    object LoadObject(Locale locale);
}

/// <summary>
/// Gives a subtree a <see cref="Locale"/> and the resources delegates load for it. Read the locale with <see cref="LocaleOf"/> and a resource type
/// with <see cref="Of{T}"/>; descendants rebuild when either changes. When a delegate does not support the locale, resolution falls back to the
/// same language, then to the delegate's result for <paramref name="fallbackLocale"/> (English by default).
/// </summary>
public sealed class Localizations(Locale locale, IReadOnlyList<ILocalizationsDelegate> delegates, Widget child, Locale? fallbackLocale = null, Key? key = null)
    : InheritedWidget(child, key)
{
    readonly Locale? fallbackLocale = fallbackLocale;
    readonly Dictionary<Type, object> _loaded = new();

    public Locale Locale { get; } = locale;
    public IReadOnlyList<ILocalizationsDelegate> Delegates { get; } = delegates;
    Locale Fallback => fallbackLocale ?? Locale.English;

    public override bool UpdateShouldNotify(InheritedWidget old)
    {
        var o = (Localizations)old;
        return o.Locale != Locale || !o.Delegates.SequenceEqual(Delegates);
    }

    /// <summary>
    /// Re-establishes the localizations of <paramref name="origin"/> around <paramref name="child"/>, for overlay entries (dialogs, menus) that sit
    /// outside the app's scope. Returns <paramref name="child"/> unchanged when there are none.
    /// </summary>
    public static Widget Wrap(BuildContext origin, Widget child) =>
        origin.DependOn<Localizations>() is { } l ? new Localizations(l.Locale, l.Delegates, child, l.fallbackLocale) : child;

    /// <summary>The nearest locale, or English when the tree has no <see cref="Localizations"/>.</summary>
    public static Locale LocaleOf(BuildContext context) => context.DependOn<Localizations>()?.Locale ?? Locale.English;

    /// <summary>The nearest resources of type <typeparamref name="T"/>, or <c>null</c> when no <see cref="Localizations"/> above has a delegate for it.</summary>
    public static T? Of<T>(BuildContext context) where T : class => context.DependOn<Localizations>()?.Resolve<T>();

    T? Resolve<T>() where T : class
    {
        if (_loaded.TryGetValue(typeof(T), out var cached)) return (T)cached;
        var d = Delegates.FirstOrDefault(x => x.ResourceType == typeof(T));
        if (d is null) return null;
        var target = Locale;
        if (!d.IsSupported(target))
        {
            var sameLanguage = new Locale(target.Language);
            target = d.IsSupported(sameLanguage) ? sameLanguage : Fallback;
        }
        var value = (T)d.LoadObject(target);
        _loaded[typeof(T)] = value;
        return value;
    }
}

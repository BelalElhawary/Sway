namespace Sway.Widgets;

/// <summary>A history entry became current without the app asking for it (browser back/forward, a deep link, an OS gesture).</summary>
public readonly record struct LocationChange(string Location, int Index);

/// <summary>
/// The app's address and its history, as seen by the <see cref="Router"/>. A location is an absolute path with an optional query
/// (<c>/products/42?color=red</c>). Each platform package supplies an implementation and installs it through <see cref="AppLocation.Source"/>:
/// the browser host maps it onto the address bar and the History API, the other hosts keep the history in memory.
/// </summary>
public interface ILocationSource
{
    /// <summary>The current location.</summary>
    string Current { get; }

    /// <summary>Ordinal of the current history entry; the first entry is 0 and each <see cref="Push"/> adds one.</summary>
    int Index { get; }

    /// <summary>Raised when the current entry changed for a reason other than <see cref="Push"/> or <see cref="Replace"/>.</summary>
    event Action<LocationChange>? Changed;

    /// <summary>Adds a history entry (and drops any forward entries).</summary>
    void Push(string location);

    /// <summary>Rewrites the current history entry.</summary>
    void Replace(string location);

    /// <summary>Moves one entry back and raises <see cref="Changed"/>; does nothing at the first entry.</summary>
    void Back();

    /// <summary>Names the current entry (the browser tab title). Hosts without a title ignore it.</summary>
    void SetTitle(string title) { }
}

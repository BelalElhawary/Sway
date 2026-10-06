using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Sway.Widgets;

/// <summary>
/// Maps the app's location onto the browser's address bar with Blazor's <see cref="NavigationManager"/> (History API, honours <c>&lt;base href&gt;</c>).
/// Each history entry carries its ordinal in the entry state, so back, forward and reload report which entry became current.
/// The server must answer deep links with <c>index.html</c> (every static host and <c>dotnet run</c> does this for a Blazor WebAssembly app).
/// </summary>
sealed class WebLocationSource : ILocationSource, IDisposable
{
    readonly NavigationManager _navigation;
    readonly IJSRuntime _js;
    string _current;
    int _index;
    string? _title;
    IJSObjectReference? _module;

    public WebLocationSource(NavigationManager navigation, IJSRuntime js)
    {
        _navigation = navigation;
        _js = js;
        _current = Read(navigation.Uri);
        _index = int.TryParse(navigation.HistoryEntryState, out var index) ? index : 0;
        navigation.LocationChanged += OnLocationChanged;
    }

    public string Current => _current;
    public int Index => _index;
    public event Action<LocationChange>? Changed;

    public void Push(string location)
    {
        _current = location;
        _index++;
        Navigate(location, replace: false);
    }

    public void Replace(string location)
    {
        _current = location;
        Navigate(location, replace: true);
    }

    public void Back() => _ = _js.InvokeVoidAsync("history.back");

    /// <summary>The sway.js module, available once the view has loaded it; a title set before that is applied then.</summary>
    public IJSObjectReference? Module
    {
        get => _module;
        set
        {
            _module = value;
            if (_title is not null) SetTitle(_title);
        }
    }

    public void SetTitle(string title)
    {
        _title = title;
        if (_module is not null) _ = _module.InvokeVoidAsync("setTitle", title);
    }

    void Navigate(string location, bool replace) =>
        // Relative to <base href>, so an app hosted under a sub-path keeps its prefix.
        _navigation.NavigateTo(location.TrimStart('/'), new NavigationOptions { ReplaceHistoryEntry = replace, HistoryEntryState = _index.ToString() });

    void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        string location = Read(e.Location);
        int index = int.TryParse(e.HistoryEntryState, out var parsed) ? parsed : 0;
        // Our own Push/Replace echo back through here; only react to entries we did not just write.
        if (index == _index && Uri.UnescapeDataString(location) == Uri.UnescapeDataString(_current)) return;
        _current = location;
        _index = index;
        Changed?.Invoke(new LocationChange(location, index));
    }

    /// <summary>The path and query of an absolute URL, relative to the app's base path.</summary>
    string Read(string uri) => "/" + _navigation.ToBaseRelativePath(uri);

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}

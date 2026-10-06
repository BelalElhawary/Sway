namespace Sway.Widgets;

/// <summary>A history kept in memory: the source on desktop, Android and headless hosts, and in tests.</summary>
public sealed class MemoryLocationSource : ILocationSource
{
    readonly List<string> _entries;
    int _index;

    public MemoryLocationSource(string initial = "/") => _entries = [initial];

    public string Current => _entries[_index];
    public int Index => _index;
    public string Title { get; private set; } = "";
    public bool CanGoForward => _index < _entries.Count - 1;

    public event Action<LocationChange>? Changed;

    public void Push(string location)
    {
        _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(location);
        _index++;
    }

    public void Replace(string location) => _entries[_index] = location;

    public void Back()
    {
        if (_index == 0) return;
        _index--;
        Changed?.Invoke(new LocationChange(Current, _index));
    }

    public void Forward()
    {
        if (!CanGoForward) return;
        _index++;
        Changed?.Invoke(new LocationChange(Current, _index));
    }

    /// <summary>Opens <paramref name="location"/> as if the OS had launched the app with it (a deep link): a new entry, announced through <see cref="Changed"/>.</summary>
    public void Open(string location)
    {
        Push(location);
        Changed?.Invoke(new LocationChange(Current, _index));
    }

    public void SetTitle(string title) => Title = title;
}

namespace Sway.Widgets;

/// <summary>
/// A file the user chose. <see cref="Source"/> is what to hand to a media player or loader: a file path on
/// desktop, a <c>content://</c> URI on Android and a <c>blob:</c> URL in the browser. <see cref="Path"/> is set only
/// where a real file path exists.
/// </summary>
public sealed class PickedFile
{
    readonly Func<Task<Stream>>? _open;

    public PickedFile(string name, string source, string? path = null, long? length = null, Func<Task<Stream>>? open = null)
    {
        Name = name;
        Source = source;
        Path = path;
        Length = length;
        _open = open;
    }

    public string Name { get; }
    public string Source { get; }
    public string? Path { get; }
    public long? Length { get; }

    /// <summary>Opens the contents for reading. Works on every platform, unlike <see cref="Path"/>.</summary>
    public Task<Stream> OpenReadAsync() =>
        _open?.Invoke() ?? (Path is not null
            ? Task.FromResult<Stream>(File.OpenRead(Path))
            : throw new NotSupportedException("This file cannot be opened for reading."));

    public static PickedFile FromPath(string path)
    {
        var info = new FileInfo(path);
        return new PickedFile(info.Name, path, path, info.Exists ? info.Length : null);
    }

    public override string ToString() => Name;
}

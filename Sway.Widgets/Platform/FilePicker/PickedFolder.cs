namespace Sway.Widgets;

/// <summary>A folder the user chose. List its files with <see cref="GetFilesAsync"/>; paths are not available on every platform.</summary>
public sealed class PickedFolder
{
    readonly Func<FileTypeFilter?, bool, Task<IReadOnlyList<PickedFile>>> _list;

    public PickedFolder(string name, string source, string? path, Func<FileTypeFilter?, bool, Task<IReadOnlyList<PickedFile>>> list)
    {
        Name = name;
        Source = source;
        Path = path;
        _list = list;
    }

    public string Name { get; }
    public string Source { get; }
    public string? Path { get; }

    /// <summary>The files in the folder, sorted by name, optionally narrowed to one type and including subfolders.</summary>
    public Task<IReadOnlyList<PickedFile>> GetFilesAsync(FileTypeFilter? filter = null, bool recursive = false) => _list(filter, recursive);

    public static PickedFolder FromPath(string path) => new(
        new DirectoryInfo(path).Name is { Length: > 0 } n ? n : path, path, path,
        (filter, recursive) => Task.FromResult<IReadOnlyList<PickedFile>>(
            Directory.EnumerateFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(f => filter is null || filter.Matches(f))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(PickedFile.FromPath)
                .ToList()));

    public override string ToString() => Name;
}

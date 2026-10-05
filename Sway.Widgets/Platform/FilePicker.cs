namespace Sway.Widgets;

/// <summary>A named set of file extensions offered in an open dialog, such as "Video" with mp4, mkv and webm.</summary>
public sealed class FileTypeFilter
{
    public FileTypeFilter(string name, params string[] extensions)
    {
        Name = name;
        Extensions = extensions.Select(e => e.TrimStart('*', '.').ToLowerInvariant()).ToArray();
    }

    public string Name { get; }

    /// <summary>Extensions without the dot or wildcard, lower case.</summary>
    public IReadOnlyList<string> Extensions { get; }

    public bool Matches(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.');
        return Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    public static FileTypeFilter Video { get; } = new("Video", "mp4", "m4v", "mkv", "webm", "avi", "mov", "wmv", "flv", "mpg", "mpeg", "ts", "ogv", "3gp");
    public static FileTypeFilter Audio { get; } = new("Audio", "mp3", "m4a", "aac", "flac", "wav", "ogg", "oga", "opus", "wma", "aiff", "mka");
    public static FileTypeFilter Images { get; } = new("Images", "png", "jpg", "jpeg", "gif", "webp", "bmp");

    /// <summary>Video and audio together, for pickers that feed a media player.</summary>
    public static FileTypeFilter Media { get; } = new("Media", [.. Video.Extensions, .. Audio.Extensions]);
}

public sealed record FilePickerOptions
{
    public string? Title { get; init; }
    public bool AllowMultiple { get; init; }

    /// <summary>The types to offer. Empty or null means every file. Hosts without a filter UI apply the first one.</summary>
    public IReadOnlyList<FileTypeFilter>? Filters { get; init; }

    /// <summary>A starting directory, honoured where the platform lets an app choose one.</summary>
    public string? InitialDirectory { get; init; }
}

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

/// <summary>
/// Shows the platform's own open-file and open-folder dialogs. Each host package supplies an implementation (Windows
/// common dialogs, zenity/kdialog on Linux, osascript on macOS, the Storage Access Framework on Android, file inputs
/// in the browser) and installs it through <see cref="FilePicker.Source"/>.
/// </summary>
public interface IFilePickerSource
{
    bool CanPickFolders { get; }

    /// <summary>Shows the dialog and returns the chosen files, or an empty list if the user cancelled.</summary>
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options);

    /// <summary>Shows the folder dialog and returns the chosen folder, or null if the user cancelled.</summary>
    Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null);
}

/// <summary>
/// The active <see cref="IFilePickerSource"/>. Call these from the UI thread, and expect the continuation to run on
/// another one: marshal back with <see cref="WidgetsBinding.Post"/> before touching widgets or state, or use
/// the callback overloads, which do it for you. Without a platform package nothing can be picked.
/// </summary>
public static class FilePicker
{
    sealed class Fallback : IFilePickerSource
    {
        public bool CanPickFolders => false;
        public Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options) => Task.FromResult<IReadOnlyList<PickedFile>>([]);
        public Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null) => Task.FromResult<PickedFolder?>(null);
    }

    /// <summary>Set by the platform host at startup.</summary>
    public static IFilePickerSource Source { get; set; } = new Fallback();

    /// <summary>False when no platform package has installed a picker.</summary>
    public static bool IsSupported => Source is not Fallback;

    public static bool CanPickFolders => Source.CanPickFolders;

    public static Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions? options = null) => Source.PickFilesAsync(options ?? new());

    public static async Task<PickedFile?> PickFileAsync(FilePickerOptions? options = null) =>
        (await Source.PickFilesAsync((options ?? new()) with { AllowMultiple = false })).FirstOrDefault();

    public static Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null) => Source.PickFolderAsync(title, initialDirectory);

    /// <summary>Shows the file dialog and calls <paramref name="onPicked"/> on the UI thread with the files (empty if cancelled).</summary>
    public static void PickFiles(Action<IReadOnlyList<PickedFile>> onPicked, FilePickerOptions? options = null) =>
        Deliver(PickFilesAsync(options), onPicked, []);

    /// <summary>Shows the folder dialog and calls <paramref name="onPicked"/> on the UI thread with the folder (null if cancelled).</summary>
    public static void PickFolder(Action<PickedFolder?> onPicked, string? title = null, string? initialDirectory = null) =>
        Deliver(PickFolderAsync(title, initialDirectory), onPicked, null);

    // A dialog that failed to open reads as a cancel.
    static void Deliver<T>(Task<T> task, Action<T> onDone, T fallback)
    {
        var binding = WidgetsBinding.Instance;
        task.ContinueWith(t => binding.Post(() => onDone(t.IsCompletedSuccessfully ? t.Result : fallback)), TaskScheduler.Default);
    }
}

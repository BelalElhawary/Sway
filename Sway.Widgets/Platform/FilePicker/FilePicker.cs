namespace Sway.Widgets;

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

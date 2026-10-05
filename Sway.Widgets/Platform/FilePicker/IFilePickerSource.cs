namespace Sway.Widgets;

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

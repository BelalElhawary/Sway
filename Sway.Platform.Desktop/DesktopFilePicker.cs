using System.Diagnostics;

namespace Sway.Widgets;

/// <summary>
/// Base for the desktop host's file dialogs, which return real paths: <see cref="WindowsFilePicker"/>, <see cref="LinuxFilePicker"/>
/// and <see cref="MacFilePicker"/>.
/// </summary>
public abstract class DesktopFilePicker : IFilePickerSource
{
    public static IFilePickerSource ForCurrentOS() =>
        OperatingSystem.IsWindows() ? new WindowsFilePicker()
        : OperatingSystem.IsMacOS() ? new MacFilePicker()
        : new LinuxFilePicker();

    public bool CanPickFolders => true;

    public abstract Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options);
    protected abstract Task<string?> PickFolderPathAsync(string? title, string? initialDirectory);

    public async Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null) =>
        await PickFolderPathAsync(title, initialDirectory) is { Length: > 0 } path && Directory.Exists(path) ? PickedFolder.FromPath(path) : null;

    protected static IReadOnlyList<PickedFile> ToFiles(IEnumerable<string> paths) =>
        paths.Where(p => p.Length > 0 && File.Exists(p)).Select(PickedFile.FromPath).ToList();

    /// <summary>Runs a helper program and returns its output lines, or null if it was cancelled, failed or is not installed.</summary>
    protected static async Task<string[]?> RunAsync(string program, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in arguments) info.ArgumentList.Add(a);
        try
        {
            using var process = Process.Start(info);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) return null;
            return (await output).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // not installed
        }
    }

}

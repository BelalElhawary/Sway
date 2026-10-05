namespace Sway.Widgets;

/// <summary>The standard macOS choosers, driven through <c>osascript</c>.</summary>
public sealed class MacFilePicker : DesktopFilePicker
{
    public override async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options)
    {
        var extensions = options.Filters?.SelectMany(f => f.Extensions).Distinct().ToArray() ?? [];
        var chooser = "choose file" + Prompt(options.Title) + Location(options.InitialDirectory)
            + (extensions.Length > 0 ? " of type {" + string.Join(", ", extensions.Select(e => Quote(e))) + "}" : "")
            + " with multiple selections allowed";
        var lines = await RunAsync("osascript",
        [
            "-e", $"set chosen to {chooser}",
            "-e", "set out to \"\"",
            "-e", "repeat with f in chosen",
            "-e", "set out to out & POSIX path of f & linefeed",
            "-e", "end repeat",
            "-e", "return out",
        ]);
        var files = ToFiles(lines ?? []);
        return options.AllowMultiple ? files : files.Take(1).ToList();
    }

    protected override async Task<string?> PickFolderPathAsync(string? title, string? initialDirectory) =>
        (await RunAsync("osascript", ["-e", $"POSIX path of (choose folder{Prompt(title)}{Location(initialDirectory)})"]))?.FirstOrDefault();

    static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string Prompt(string? title) => title is { Length: > 0 } ? " with prompt " + Quote(title) : "";
    static string Location(string? dir) => dir is not null && Directory.Exists(dir) ? " default location (POSIX file " + Quote(dir) + ")" : "";
}

namespace Sway.Widgets;

/// <summary>Linux dialogs through <c>zenity</c>, falling back to <c>kdialog</c>. Needs one of them installed.</summary>
public sealed class LinuxFilePicker : DesktopFilePicker
{
    public override async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options)
    {
        var filters = options.Filters ?? [];
        var zenity = new List<string> { "--file-selection", "--separator=\n" };
        if (options.Title is not null) zenity.Add("--title=" + options.Title);
        if (options.AllowMultiple) zenity.Add("--multiple");
        if (InitialPath(options.InitialDirectory) is { } dir) zenity.Add("--filename=" + dir);
        foreach (var f in filters) zenity.Add($"--file-filter={f.Name} | {string.Join(' ', f.Extensions.Select(e => "*." + e))}");
        if (filters.Count > 0) zenity.Add("--file-filter=All files | *");

        var lines = await RunAsync("zenity", zenity);
        if (lines is null && !await HasProgram("zenity"))
        {
            var kdialog = new List<string> { "--getopenfilename", InitialPath(options.InitialDirectory) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
            kdialog.Add(filters.Count > 0 ? string.Join(' ', filters.SelectMany(f => f.Extensions).Select(e => "*." + e)) + "|" + string.Join(", ", filters.Select(f => f.Name)) : "*");
            if (options.AllowMultiple) kdialog.AddRange(["--multiple", "--separate-output"]);
            if (options.Title is not null) kdialog.AddRange(["--title", options.Title]);
            lines = await RunAsync("kdialog", kdialog);
        }
        return ToFiles(lines ?? []);
    }

    protected override async Task<string?> PickFolderPathAsync(string? title, string? initialDirectory)
    {
        var zenity = new List<string> { "--file-selection", "--directory" };
        if (title is not null) zenity.Add("--title=" + title);
        if (InitialPath(initialDirectory) is { } dir) zenity.Add("--filename=" + dir);
        var lines = await RunAsync("zenity", zenity);
        if (lines is null && !await HasProgram("zenity"))
            lines = await RunAsync("kdialog", ["--getexistingdirectory", InitialPath(initialDirectory) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]);
        return lines?.FirstOrDefault();
    }

    static string? InitialPath(string? dir) => dir is not null && Directory.Exists(dir) ? Path.TrimEndingDirectorySeparator(dir) + "/" : null;

    // RunAsync reports a cancel and a missing program the same way, so ask the shell which one it was.
    static async Task<bool> HasProgram(string name) => await RunAsync("which", [name]) is { Length: > 0 };
}

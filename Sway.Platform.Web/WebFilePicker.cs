using Microsoft.JSInterop;

namespace Sway.Widgets;

/// <summary>
/// The browser's file dialogs, through a hidden file input. A page cannot see real paths, so a picked file's
/// <see cref="PickedFile.Source"/> is a <c>blob:</c> URL (which <c>Sway.Media</c> can play) and its folder is
/// the list of files the browser exposed. Pickers must be started from a click or key press.
/// </summary>
sealed class WebFilePicker(IJSObjectReference module) : IFilePickerSource
{
    sealed record WebFile(string Name, string Url, long Size, string RelativePath);

    public bool CanPickFolders => true;

    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options)
    {
        var accept = options.Filters is { Count: > 0 } filters
            ? string.Join(',', filters.SelectMany(f => f.Extensions).Select(e => "." + e))
            : "";
        var files = await module.InvokeAsync<WebFile[]>("pickFiles", accept, options.AllowMultiple);
        return files.Select(ToPicked).ToList();
    }

    public async Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null)
    {
        var files = await module.InvokeAsync<WebFile[]>("pickFolder");
        if (files.Length == 0) return null;

        string name = files[0].RelativePath.Split('/')[0];
        return new PickedFolder(name, name, null, (filter, recursive) =>
        {
            // RelativePath is "folder/sub/file.ext": one separator means a direct child.
            var matches = files
                .Where(f => recursive || f.RelativePath.Count(c => c == '/') <= 1)
                .Where(f => filter is null || filter.Matches(f.Name))
                .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(ToPicked)
                .ToList();
            return Task.FromResult<IReadOnlyList<PickedFile>>(matches);
        });
    }

    PickedFile ToPicked(WebFile f) => new(f.Name, f.Url, null, f.Size, async () => new MemoryStream(await module.InvokeAsync<byte[]>("readBytes", f.Url)));
}

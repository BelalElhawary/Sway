using Android.App;
using Android.Content;
using Android.Provider;
using Android.Webkit;

namespace Sway.Widgets;

/// <summary>
/// The Storage Access Framework pickers (<c>ACTION_OPEN_DOCUMENT</c> and <c>ACTION_OPEN_DOCUMENT_TREE</c>). Android has
/// no file paths for these: a picked file's <see cref="PickedFile.Source"/> is a <c>content://</c> URI, which
/// <see cref="PickedFile.OpenReadAsync"/> and <c>Sway.Media</c> both know how to read.
/// </summary>
sealed class AndroidFilePicker(SwayActivity activity) : IFilePickerSource
{
    ContentResolver Resolver => activity.ContentResolver!;

    public bool CanPickFolders => true;

    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options)
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, options.AllowMultiple);
        if (MimeTypes(options.Filters) is { Length: > 0 } mimes) intent.PutExtra(Intent.ExtraMimeTypes, mimes);

        var data = await activity.StartForResultAsync(intent);
        if (data is null) return [];

        var uris = new List<global::Android.Net.Uri>();
        if (data.ClipData is { } clip)
            for (int i = 0; i < clip.ItemCount; i++) { if (clip.GetItemAt(i)?.Uri is { } u) uris.Add(u); }
        else if (data.Data is { } single) uris.Add(single);

        return uris.Select(u => Describe(u, null)).ToList();
    }

    public async Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null)
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);

        var data = await activity.StartForResultAsync(intent);
        if (data?.Data is not { } tree) return null;
        try { Resolver.TakePersistableUriPermission(tree, ActivityFlags.GrantReadUriPermission); } catch (Exception) { /* the grant lasts for this session regardless */ }

        string rootId = DocumentsContract.GetTreeDocumentId(tree)!;
        string name = Query(DocumentsContract.BuildDocumentUriUsingTree(tree, rootId)!, [IOpenableColumns.DisplayName])?.FirstOrDefault()?.GetValueOrDefault(IOpenableColumns.DisplayName) ?? "Folder";
        return new PickedFolder(name, tree.ToString()!, null, (filter, recursive) => Task.Run<IReadOnlyList<PickedFile>>(() =>
        {
            var found = new List<PickedFile>();
            Walk(tree, rootId, filter, recursive, found);
            return found.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }));
    }

    void Walk(global::Android.Net.Uri tree, string parentId, FileTypeFilter? filter, bool recursive, List<PickedFile> found)
    {
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, parentId)!;
        var rows = Query(children, [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize]) ?? [];
        foreach (var row in rows)
        {
            string id = row[DocumentsContract.Document.ColumnDocumentId];
            string name = row[DocumentsContract.Document.ColumnDisplayName];
            if (row[DocumentsContract.Document.ColumnMimeType] == DocumentsContract.Document.MimeTypeDir)
            {
                if (recursive) Walk(tree, id, filter, recursive, found);
                continue;
            }
            if (filter is not null && !filter.Matches(name)) continue;
            long? size = long.TryParse(row[DocumentsContract.Document.ColumnSize], out var n) ? n : null;
            found.Add(Describe(DocumentsContract.BuildDocumentUriUsingTree(tree, id)!, (name, size)));
        }
    }

    PickedFile Describe(global::Android.Net.Uri uri, (string Name, long? Size)? known)
    {
        try { Resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission); } catch (Exception) { /* not every provider offers a persistable grant */ }
        var (name, size) = known ?? QueryNameAndSize(uri);
        return new PickedFile(name, uri.ToString()!, null, size, () => Task.FromResult(Resolver.OpenInputStream(uri) ?? throw new IOException("The file could not be opened.")));
    }

    (string Name, long? Size) QueryNameAndSize(global::Android.Net.Uri uri)
    {
        var row = Query(uri, [IOpenableColumns.DisplayName, IOpenableColumns.Size])?.FirstOrDefault();
        string name = row?.GetValueOrDefault(IOpenableColumns.DisplayName) ?? uri.LastPathSegment ?? "file";
        return (name, long.TryParse(row?.GetValueOrDefault(IOpenableColumns.Size), out var n) ? n : null);
    }

    // Cursor rows as column -> text, so callers do not juggle column indexes.
    List<Dictionary<string, string>>? Query(global::Android.Net.Uri uri, string[] columns)
    {
        using var cursor = Resolver.Query(uri, columns, null, null, null);
        if (cursor is null) return null;
        var rows = new List<Dictionary<string, string>>();
        while (cursor.MoveToNext())
        {
            var row = new Dictionary<string, string>();
            foreach (var column in columns)
            {
                int index = cursor.GetColumnIndex(column);
                row[column] = index >= 0 ? cursor.GetString(index) ?? "" : "";
            }
            rows.Add(row);
        }
        return rows;
    }

    static string[]? MimeTypes(IReadOnlyList<FileTypeFilter>? filters)
    {
        if (filters is not { Count: > 0 }) return null;
        var mimes = filters.SelectMany(f => f.Extensions)
            .Select(e => MimeTypeMap.Singleton?.GetMimeTypeFromExtension(e))
            .Where(m => m is not null).Distinct().Select(m => m!).ToArray();
        return mimes;
    }
}

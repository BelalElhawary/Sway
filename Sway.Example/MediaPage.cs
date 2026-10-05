using Sway.Media;
using Sway.Extras.Material3;
using Sway.Extras.Material3.Media;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>
/// The video and audio players, plus the platform file and folder pickers feeding them. Set SWAY_MEDIA to preload a
/// file or URL (SWAY_MEDIA_AUTOPLAY=1 starts it).
/// </summary>
class MediaPage : StatefulWidget
{
    public override State CreateState() => new MediaPageState();
}

sealed class MediaPageState : State<MediaPage>
{
    static MediaPageState() => MediaPlayerController.Preload();

    readonly MediaPlayerController _video = new();
    readonly MediaPlayerController _audio = new();
    readonly TextEditingController _source = new(Environment.GetEnvironmentVariable("SWAY_MEDIA") ?? "");

    PickedFolder? _folder;
    IReadOnlyList<PickedFile> _files = [];
    string? _playing;

    public override void InitState()
    {
        if (_source.Text.Length > 0) PlaySource(_source.Text, _source.Text, Environment.GetEnvironmentVariable("SWAY_MEDIA_AUTOPLAY") == "1");
    }

    public override void Dispose()
    {
        _video.Dispose();
        _audio.Dispose();
    }

    public override Widget Build(BuildContext context) => Ui.Page("Media", [
        Ui.Section(context, "Source", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
        [
            new Row(spacing: 8, children:
            [
                new Expanded(new TextField(_source, decoration: new InputDecoration(LabelText: "File path or URL"), onSubmitted: s => PlaySource(s, s, true))),
                new FilledButton(new Text("Play"), () => PlaySource(_source.Text, _source.Text, true)),
            ]),
            new Row(spacing: 8, children:
            [
                new FilledTonalButton(new Text("Open file"), OpenFile, Icons.Folder),
                ..FilePicker.CanPickFolders ? [new FilledTonalButton(new Text("Open folder"), OpenFolder, Icons.FolderOpen)] : Array.Empty<Widget>(),
                ..FilePicker.IsSupported ? Array.Empty<Widget>() : [new Text("No file picker on this host")],
            ]),
        ]), "Type a path or URL, or use the platform's own file and folder dialogs"),

        Ui.Section(context, "VideoPlayer", new AspectRatio(16f / 9, new ClipRRect(BorderRadius.Circular(12), new VideoPlayer(_video)))),

        Ui.Section(context, "AudioPlayer", new AudioPlayer(_audio)),

        ..FilePicker.CanPickFolders && _folder is not null ? [Ui.Section(context, _folder.Name, FileList(), $"{_files.Count} media files")] : Array.Empty<Widget>(),
    ]);

    Widget FileList() => _files.Count == 0
        ? new Text("No audio or video files in this folder")
        : new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            _files.Select(f => (Widget)new ListTile(
                title: new Text(f.Name, maxLines: 1, overflow: TextOverflow.Ellipsis),
                leading: new Icon(FileTypeFilter.Audio.Matches(f.Name) ? Icons.MusicNote : Icons.PlayArrow),
                trailing: f.Source == _playing ? new Icon(Icons.VolumeUp) : null,
                onTap: () => PlaySource(f.Source, f.Name, true))).ToList());

    void OpenFile() => FilePicker.PickFiles(files =>
    {
        if (files.Count > 0) PlaySource(files[0].Source, files[0].Name, true);
    }, new FilePickerOptions { Title = "Open audio or video", Filters = [FileTypeFilter.Media] });

    // Listing a folder can be slow (a network share, a large Android tree), so it runs off the UI thread and posts back.
    void OpenFolder() => FilePicker.PickFolder(folder =>
    {
        if (folder is null) return;
        folder.GetFilesAsync(FileTypeFilter.Media).ContinueWith(t => WidgetsBinding.Instance.Post(() =>
            SetState(() => { _folder = folder; _files = t.IsCompletedSuccessfully ? t.Result : []; })));
    }, "Open a media folder");

    // Audio files go to the audio player and everything else to the video player; each keeps its own controller.
    void PlaySource(string source, string name, bool play)
    {
        if (source.Length == 0) return;
        var target = FileTypeFilter.Audio.Matches(name) ? _audio : _video;
        (target == _audio ? _video : _audio).Stop();
        target.Open(source);
        if (play) target.Play();
        _playing = source;
        SetState();
    }
}

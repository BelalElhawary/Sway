using Sway.Media;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

/// <summary>Plays an audio or video file or URL: set SWAY_MEDIA to preload one.</summary>
class MediaPage : StatefulWidget
{
    public override State CreateState() => new MediaPageState();
}

sealed class MediaPageState : State<MediaPage>
{
    readonly MediaPlayerController _player = new();
    readonly TextEditingController _source = new(Environment.GetEnvironmentVariable("SWAY_MEDIA") ?? "");

    public override void InitState()
    {
        if (_source.Text.Length > 0) _player.Open(_source.Text);
        if (Environment.GetEnvironmentVariable("SWAY_MEDIA_AUTOPLAY") == "1") _player.Play();
    }

    public override void Dispose() => _player.Dispose();

    static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    public override Widget Build(BuildContext context) => Ui.Page("Media", [
        Ui.Section(context, "Player", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
        [
            new Row(spacing: 8, children:
            [
                new Expanded(new TextField(_source, decoration: new InputDecoration(LabelText: "File path or URL"), onSubmitted: Open)),
                new FilledButton(new Text("Open"), () => Open(_source.Text)),
            ]),
            new SizedBox(height: 320, child: new ClipRRect(BorderRadius.Circular(12), new VideoPlayer(_player))),
            new MediaBuilder(_player, (ctx, p) =>
            {
                float total = (float)p.Duration.TotalSeconds;
                return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                [
                    new Slider(total > 0 ? (float)p.Position.TotalSeconds : 0, v => p.Position = TimeSpan.FromSeconds(v), 0, Math.Max(total, 1)),
                    new Row(spacing: 8, children:
                    [
                        new IconButton(new Icon(p.Status == MediaStatus.Playing ? Icons.Pause : Icons.PlayArrow), p.TogglePlay, IconButtonVariant.Filled),
                        new IconButton(new Icon(Icons.Stop), p.Stop),
                        new Text($"{Format(p.Position)} / {Format(p.Duration)}"),
                        new Spacer(),
                        new Text(p.Status.ToString()),
                    ]),
                ]);
            }),
        ])),
    ]);

    void Open(string source)
    {
        if (source.Length == 0) return;
        _player.Open(source);
        _player.Play();
    }
}

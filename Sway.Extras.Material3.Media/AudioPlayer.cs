using Sway.Extras.Material3;
using Sway.Media;
using Sway.Widgets;

namespace Sway.Extras.Material3.Media;

/// <summary>
/// A compact audio player: artwork or a note icon, title, subtitle, play/pause, seek bar, time, volume and a loop
/// toggle, in the app's theme. Give it a <paramref name="source"/> and it plays on its own, or pass a
/// <paramref name="controller"/> to drive it yourself. The title defaults to the file name.
/// </summary>
public sealed class AudioPlayer(
    MediaPlayerController? controller = null,
    string? source = null,
    string? title = null,
    string? subtitle = null,
    Widget? leading = null,
    bool autoPlay = false,
    bool looping = false,
    bool showVolume = true,
    Key? key = null) : StatefulWidget(key)
{
    internal MediaPlayerController? Controller => controller;
    internal string? Source => source;
    internal string? Title => title;
    internal string? Subtitle => subtitle;
    internal Widget? Leading => leading;
    internal bool AutoPlay => autoPlay;
    internal bool Looping => looping;
    internal bool ShowVolume => showVolume;

    public override State CreateState() => new AudioPlayerState();

    sealed class AudioPlayerState : PlayerState<AudioPlayer>
    {
        protected override MediaPlayerController? SuppliedController => Widget.Controller;
        protected override string? SourceOf(AudioPlayer w) => w.Source;
        protected override bool AutoPlay => Widget.AutoPlay;
        protected override bool Loop => Widget.Looping;

        public override Widget Build(BuildContext context)
        {
            var c = Controller;
            var theme = Theme.Of(context);
            var scheme = theme.ColorScheme;

            return new MediaBuilder(c, (ctx, p) =>
            {
                bool playing = p.Status is MediaStatus.Playing or MediaStatus.Buffering or MediaStatus.Opening;
                string name = Widget.Title ?? MediaFormat.Name(p.Source ?? Widget.Source);
                string? line2 = p.Status switch
                {
                    MediaStatus.Error => "This media can't be played",
                    MediaStatus.Buffering => $"Buffering {p.BufferingProgress:0}%",
                    _ => Widget.Subtitle,
                };
                var muted = new TextStyle(Color: scheme.OnSurfaceVariant);

                Widget art = Widget.Leading ?? new Container(
                    width: 56, height: 56, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: scheme.PrimaryContainer, BorderRadius: BorderRadius.Circular(12)),
                    child: new Icon(Icons.MusicNote, 28, scheme.OnPrimaryContainer));

                return new Container(
                    padding: EdgeInsets.Symmetric(horizontal: 12, vertical: 8),
                    decoration: new BoxDecoration(Color: scheme.SurfaceContainer, BorderRadius: BorderRadius.Circular(16)),
                    child: new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                    [
                        new Row(spacing: 12, children:
                        [
                            new SizedBox(width: 56, height: 56, child: new ClipRRect(BorderRadius.Circular(12), art)),
                            new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                            [
                                new Text(name.Length > 0 ? name : "No media", style: theme.TextTheme.TitleMedium, maxLines: 1, overflow: TextOverflow.Ellipsis),
                                ..line2 is { Length: > 0 } ? [new Text(line2, style: theme.TextTheme.BodyMedium.Merge(muted), maxLines: 1, overflow: TextOverflow.Ellipsis)] : Array.Empty<Widget>(),
                            ])),
                            new IconButton(new Icon(playing ? Icons.Pause : Icons.PlayArrow), p.TogglePlay, IconButtonVariant.Filled),
                        ]),
                        new MediaSeekBar(p),
                        new Row(spacing: 4, children:
                        [
                            new Padding(EdgeInsets.Only(left: 8), new MediaTimeLabel(p, theme.TextTheme.LabelSmall.Merge(muted))),
                            new Spacer(),
                            ..Widget.ShowVolume ? [new MediaVolume(p, showSlider: false)] : Array.Empty<Widget>(),
                            new IconButton(new Icon(Icons.Repeat), () => p.Looping = !p.Looping, selected: p.Looping),
                        ]),
                    ]));
            });
        }
    }
}

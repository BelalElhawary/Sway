using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Media;
using Sway.Widgets;

namespace Sway.Extras.Material3.Media;

/// <summary>
/// A complete video player: the picture, a buffering or error indicator, tap-to-pause and a control bar (play, seek,
/// time, volume) that shows on hover or while paused. Give it a <paramref name="source"/> and it plays on its own, or
/// pass a <paramref name="controller"/> to drive it yourself. Use <see cref="VideoSurface"/> for just the picture.
/// </summary>
public sealed class VideoPlayer(
    MediaPlayerController? controller = null,
    string? source = null,
    bool autoPlay = false,
    bool looping = false,
    bool showControls = true,
    VideoFit fit = VideoFit.Contain,
    SKColor? background = null,
    Action? onFullscreen = null,
    Key? key = null) : StatefulWidget(key)
{
    internal MediaPlayerController? Controller => controller;
    internal string? Source => source;
    internal bool AutoPlay => autoPlay;
    internal bool Looping => looping;
    internal bool ShowControls => showControls;
    internal VideoFit Fit => fit;
    internal SKColor Background => background ?? SKColors.Black;
    internal Action? OnFullscreen => onFullscreen;

    public override State CreateState() => new VideoPlayerState();

    sealed class VideoPlayerState : PlayerState<VideoPlayer>
    {
        bool _hover;

        protected override MediaPlayerController? SuppliedController => Widget.Controller;
        protected override string? SourceOf(VideoPlayer w) => w.Source;
        protected override bool AutoPlay => Widget.AutoPlay;
        protected override bool Loop => Widget.Looping;

        public override Widget Build(BuildContext context)
        {
            var c = Controller;
            return new MouseRegion(
                onEnter: _ => SetState(() => _hover = true),
                onExit: _ => SetState(() => _hover = false),
                child: new Stack(fit: StackFit.Expand, children:
                [
                    new GestureDetector(new VideoSurface(c, Widget.Fit, Widget.Background), onTap: Widget.ShowControls ? c.TogglePlay : null, onDoubleTap: Widget.OnFullscreen),
                    new MediaBuilder(c, (_, p) => new Center(StatusOverlay(context, p))),
                    ..Widget.ShowControls ? [new MediaBuilder(c, (ctx, p) => ControlBar(ctx, p))] : Array.Empty<Widget>(),
                ]));
        }

        static Widget StatusOverlay(BuildContext context, MediaPlayerController p)
        {
            if (p.Status == MediaStatus.Error)
                return new IgnorePointer(new Column(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
                [
                    new Icon(Icons.Error, 40, SKColors.White),
                    new Text(MaterialLocalizations.Of(context).MediaErrorLabel, style: new TextStyle(Color: SKColors.White)),
                ]));
            if (p.Status is MediaStatus.Opening or MediaStatus.Buffering)
                return new IgnorePointer(new CircularProgressIndicator(color: SKColors.White, size: 40));
            return SizedBox.Shrink();
        }

        Widget ControlBar(BuildContext ctx, MediaPlayerController p)
        {
            bool playing = p.Status is MediaStatus.Playing or MediaStatus.Buffering or MediaStatus.Opening;
            bool visible = _hover || !playing;
            var white = SKColors.White;
            var label = new TextStyle(Color: white, FontSize: 12);

            Widget bar = new LayoutBuilder((_, box) => new Container(
                padding: EdgeInsets.Only(left: 8, right: 8, top: 24),
                decoration: new BoxDecoration(Gradient: new LinearGradient([SKColors.Transparent, new SKColor(0, 0, 0, 170)], Alignment.TopCenter, Alignment.BottomCenter)),
                child: new Column(mainAxisSize: MainAxisSize.Min, children:
                [
                    new MediaSeekBar(p),
                    new Row(spacing: 4, children:
                    [
                        new IconButton(new Icon(playing ? Icons.Pause : Icons.PlayArrow, color: white), p.TogglePlay),
                        new MediaTimeLabel(p, label),
                        new Spacer(),
                        new MediaVolume(p, showSlider: box.MaxWidth >= 420, iconColor: white),
                        new IconButton(new Icon(Icons.Repeat, color: p.Looping ? Theme.Of(ctx).ColorScheme.Primary : white), () => p.Looping = !p.Looping),
                        ..Widget.OnFullscreen is { } fs ? [new IconButton(new Icon(Icons.Fullscreen, color: white), fs)] : Array.Empty<Widget>(),
                    ]),
                ])));

            return new Align(Alignment.BottomCenter, new AnimatedOpacity(visible ? 1 : 0, TimeSpan.FromMilliseconds(180), new IgnorePointer(bar, ignoring: !visible)));
        }
    }
}

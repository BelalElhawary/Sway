using SkiaSharp;
using Sway.Widgets;

namespace Sway.Media;

public enum VideoFit { Contain, Cover, Fill }

/// <summary>
/// Rebuilds <paramref name="builder"/> on every frame while the player is working, and once whenever a control
/// method changes it. Use it for progress bars and play buttons that follow the player.
/// </summary>
public sealed class MediaBuilder(MediaPlayerController controller, Func<BuildContext, MediaPlayerController, Widget> builder, Key? key = null)
    : StatefulWidget(key)
{
    internal MediaPlayerController Controller => controller;
    internal Func<BuildContext, MediaPlayerController, Widget> Builder => builder;

    public override State CreateState() => new MediaBuilderState();

    sealed class MediaBuilderState : State<MediaBuilder>
    {
        bool _pumping;

        public override void InitState() => Attach(Widget.Controller);

        public override void DidUpdateWidget(MediaBuilder old)
        {
            if (old.Controller == Widget.Controller) return;
            old.Controller.Changed -= OnChanged;
            Attach(Widget.Controller);
        }

        public override void Dispose() => Widget.Controller.Changed -= OnChanged;

        void Attach(MediaPlayerController c)
        {
            c.Changed += OnChanged;
            if (c.IsActive) Pump();
        }

        void OnChanged()
        {
            SetState();
            Pump();
        }

        // The player's own threads never touch widgets, so the UI polls it once per frame instead.
        void Pump()
        {
            if (_pumping) return;
            _pumping = true;
            WidgetsBinding.Instance.ScheduleFrameCallback(_ =>
            {
                _pumping = false;
                if (!Mounted) return;
                SetState();
                if (Widget.Controller.IsActive) Pump();
            });
        }

        public override Widget Build(BuildContext context) => Widget.Builder(context, Widget.Controller);
    }
}

/// <summary>Draws the current video frame of a controller, scaled to fit. Audio-only media draws just the background.</summary>
public sealed class VideoPlayer(MediaPlayerController controller, VideoFit fit = VideoFit.Contain, SKColor? background = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) =>
        new MediaBuilder(controller, (_, c) => SizedBox.Expand(new CustomPaint(new VideoPainter(c, fit, background ?? SKColors.Black))));

    sealed class VideoPainter(MediaPlayerController controller, VideoFit fit, SKColor background) : CustomPainter
    {
        public override void Paint(SKCanvas canvas, Size size)
        {
            using (var fill = new SKPaint { Color = background }) canvas.DrawRect(0, 0, size.Width, size.Height, fill);
            if (controller.CurrentFrame is not { } frame) return;

            float fw = frame.Width, fh = frame.Height;
            float scale = fit switch
            {
                VideoFit.Cover => Math.Max(size.Width / fw, size.Height / fh),
                VideoFit.Fill => 1,
                _ => Math.Min(size.Width / fw, size.Height / fh),
            };
            float w = fit == VideoFit.Fill ? size.Width : fw * scale, h = fit == VideoFit.Fill ? size.Height : fh * scale;
            var dest = new SKRect((size.Width - w) / 2, (size.Height - h) / 2, (size.Width + w) / 2, (size.Height + h) / 2);

            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));
            using var image = SKImage.FromBitmap(frame);
            canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear));
            canvas.Restore();
        }

        public override bool ShouldRepaint(CustomPainter old) => true;
    }
}

using SkiaSharp;
using Sway.Widgets;

namespace Sway.Media;

static class MediaFormat
{
    public static string Time(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    /// <summary>A display name for a source: the file name of a path or URL, without the query.</summary>
    public static string Name(string? source)
    {
        if (string.IsNullOrEmpty(source)) return "";
        var s = source;
        int cut = s.IndexOfAny(['?', '#']);
        if (cut >= 0 && s.Contains("://", StringComparison.Ordinal)) s = s[..cut];
        s = Uri.UnescapeDataString(s.TrimEnd('/', '\\'));
        int slash = s.LastIndexOfAny(['/', '\\']);
        return slash >= 0 ? s[(slash + 1)..] : s;
    }
}

/// <summary>A seek slider that follows the player and only seeks when the thumb is released, so scrubbing stays smooth.</summary>
sealed class MediaSeekBar(MediaPlayerController controller) : StatefulWidget
{
    internal MediaPlayerController Controller => controller;

    public override State CreateState() => new SeekBarState();

    sealed class SeekBarState : State<MediaSeekBar>
    {
        float? _drag;

        public override Widget Build(BuildContext context) => new MediaBuilder(Widget.Controller, (_, c) =>
        {
            float total = (float)c.Duration.TotalSeconds;
            float max = Math.Max(total, 1);
            float value = _drag ?? (total > 0 ? (float)c.Position.TotalSeconds : 0);
            return new Slider(Math.Clamp(value, 0, max),
                v => SetState(() => _drag = v), 0, max,
                onChangeEnd: v =>
                {
                    if (total > 0) c.Position = TimeSpan.FromSeconds(v);
                    SetState(() => _drag = null);
                });
        });
    }
}

/// <summary>Mute button plus a volume slider; the slider is left out when <paramref name="showSlider"/> is false.</summary>
sealed class MediaVolume(MediaPlayerController controller, bool showSlider = true, SKColor? iconColor = null) : StatelessWidget
{
    public override Widget Build(BuildContext context) => new MediaBuilder(controller, (_, c) =>
    {
        bool silent = c.Muted || c.Volume == 0;
        var children = new List<Widget>
        {
            new IconButton(new Icon(silent ? Icons.VolumeOff : Icons.VolumeUp, color: iconColor), () => c.Muted = !c.Muted),
        };
        if (showSlider)
            children.Add(new SizedBox(width: 96, child: new Slider(c.Muted ? 0 : Math.Min(c.Volume, 100), v => { c.Muted = false; c.Volume = (int)v; }, 0, 100)));
        return new Row(mainAxisSize: MainAxisSize.Min, children: children);
    });
}

/// <summary>"1:23 / 4:56", following the player.</summary>
sealed class MediaTimeLabel(MediaPlayerController controller, TextStyle? style = null) : StatelessWidget
{
    public override Widget Build(BuildContext context) => new MediaBuilder(controller, (_, c) =>
        new Text($"{MediaFormat.Time(c.Position)} / {MediaFormat.Time(c.Duration)}", style: style, maxLines: 1));
}

/// <summary>Owns a controller unless the caller passed one, and applies the source, looping and autoplay options to it.</summary>
abstract class PlayerState<T> : State<T> where T : StatefulWidget
{
    protected abstract MediaPlayerController? SuppliedController { get; }
    protected abstract string? SourceOf(T widget);
    protected abstract bool AutoPlay { get; }
    protected abstract bool Loop { get; }

    MediaPlayerController? _owned;
    protected MediaPlayerController Controller => SuppliedController ?? (_owned ??= new MediaPlayerController());

    public override void InitState()
    {
        var c = Controller;
        if (SuppliedController is not null) return;
        c.Looping = Loop;
        if (SourceOf(Widget) is { Length: > 0 } source) OpenAndMaybePlay(c, source);
    }

    public override void DidUpdateWidget(T old)
    {
        if (SuppliedController is not null || _owned is null) return;
        _owned.Looping = Loop;
        if (SourceOf(old) != SourceOf(Widget) && SourceOf(Widget) is { Length: > 0 } source) OpenAndMaybePlay(_owned, source);
    }

    void OpenAndMaybePlay(MediaPlayerController c, string source)
    {
        c.Open(source);
        if (AutoPlay) c.Play();
    }

    public override void Dispose() => _owned?.Dispose();
}

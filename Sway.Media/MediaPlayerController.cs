using SkiaSharp;

namespace Sway.Media;

/// <summary>
/// Plays audio or video through the platform's <see cref="IMediaBackend"/>. Call the control methods and read the
/// properties from the UI thread. <see cref="Changed"/> fires after every control call; for a continuously updating
/// position, rebuild from a <see cref="MediaBuilder"/>.
/// </summary>
public sealed class MediaPlayerController : IDisposable
{
    readonly IMediaBackend _backend = MediaBackend.Factory();
    bool _disposed;

    /// <summary>Loads the platform engine on a background thread so the first <see cref="Open"/> does not stall the UI. Call it at startup.</summary>
    public static void Preload() => MediaBackend.Preload?.Invoke();

    /// <summary>Raised on the calling thread after a control method changes the player (open, play, pause, seek, ...).</summary>
    public event Action? Changed;

    /// <summary>The file path or URL last given to <see cref="Open"/>.</summary>
    public string? Source { get; private set; }

    /// <summary>Opens a file path or a URL (http, rtsp, ...), or a picked file's <see cref="Sway.Widgets.PickedFile.Source"/>. Playback starts only after <see cref="Play"/>.</summary>
    public void Open(string source)
    {
        Source = source;
        _backend.Open(source);
        Changed?.Invoke();
    }

    public void Play() { _backend.Play(); Changed?.Invoke(); }
    public void Pause() { _backend.Pause(); Changed?.Invoke(); }
    public void Resume() { _backend.Resume(); Changed?.Invoke(); }
    public void Stop() { _backend.Stop(); Changed?.Invoke(); }

    public void TogglePlay()
    {
        if (Status is MediaStatus.Playing or MediaStatus.Buffering) Pause();
        else if (Status == MediaStatus.Paused) Resume();
        else Play();
    }

    public TimeSpan Duration => _backend.Duration;

    public TimeSpan Position
    {
        get => _backend.Position;
        set { _backend.Position = value; Changed?.Invoke(); }
    }

    /// <summary>0 to 100; values above 100 amplify where the platform allows it.</summary>
    public int Volume
    {
        get => _backend.Volume;
        set { _backend.Volume = Math.Clamp(value, 0, 200); Changed?.Invoke(); }
    }

    public bool Muted
    {
        get => _backend.Muted;
        set { _backend.Muted = value; Changed?.Invoke(); }
    }

    /// <summary>Restart from the beginning instead of ending.</summary>
    public bool Looping
    {
        get => _backend.Looping;
        set { _backend.Looping = value; Changed?.Invoke(); }
    }

    public MediaStatus Status => _backend.Status;

    /// <summary>Cache fill, 0 to 100, while the status is <see cref="MediaStatus.Buffering"/>.</summary>
    public float BufferingProgress => _backend.BufferingProgress;

    /// <summary>True while the player is working and the UI should keep asking for frames.</summary>
    public bool IsActive => Status is MediaStatus.Opening or MediaStatus.Buffering or MediaStatus.Playing;

    /// <summary>The decoded video size, or zero for audio-only media or before the first frame.</summary>
    public (int Width, int Height) VideoSize => _backend.VideoSize;

    /// <summary>
    /// The most recent video frame, or null if there is none. The bitmap is owned by the controller and replaced
    /// when a newer frame arrives, so paint it right away and do not keep it.
    /// </summary>
    public SKBitmap? CurrentFrame => _backend.CurrentFrame;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backend.Dispose();
    }
}

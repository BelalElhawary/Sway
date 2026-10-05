using SkiaSharp;

namespace Sway.Media;

public enum MediaStatus { Idle, Opening, Buffering, Playing, Paused, Ended, Error }

/// <summary>
/// The engine behind a <see cref="MediaPlayerController"/>. Each platform supplies one: LibVLC on Windows, Linux, macOS
/// and Android (<c>Sway.Media.LibVlc</c>) and an HTML media element in the browser (<c>Sway.Media.Web</c>). All members
/// are called from the UI thread and must not block it; do slow native work on your own thread.
/// </summary>
public interface IMediaBackend : IDisposable
{
    /// <summary>Opens a file path or a URL. On Android a <c>content://</c> URI and in the browser a <c>blob:</c> URL work too. Playback starts only after <see cref="Play"/>.</summary>
    void Open(string source);

    void Play();
    void Pause();
    void Resume();
    void Stop();

    TimeSpan Duration { get; }
    TimeSpan Position { get; set; }

    /// <summary>0 to 100; values above 100 amplify where the platform allows it.</summary>
    int Volume { get; set; }
    bool Muted { get; set; }

    /// <summary>Restart from the beginning instead of ending.</summary>
    bool Looping { get; set; }

    MediaStatus Status { get; }

    /// <summary>Cache fill, 0 to 100, while the status is <see cref="MediaStatus.Buffering"/>.</summary>
    float BufferingProgress { get; }

    /// <summary>The decoded video size, or zero for audio-only media or before the first frame.</summary>
    (int Width, int Height) VideoSize { get; }

    /// <summary>The newest video frame, or null. The backend owns the bitmap and may reuse it, so paint it right away and do not keep it.</summary>
    SKBitmap? CurrentFrame { get; }
}

/// <summary>The backend that <see cref="MediaPlayerController"/> uses. Each host installs one at startup.</summary>
public static class MediaBackend
{
    sealed class Unsupported : IMediaBackend
    {
        public void Open(string source) { }
        public void Play() { }
        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
        public TimeSpan Duration => TimeSpan.Zero;
        public TimeSpan Position { get => TimeSpan.Zero; set { } }
        public int Volume { get; set; } = 100;
        public bool Muted { get; set; }
        public bool Looping { get; set; }
        public MediaStatus Status => MediaStatus.Error;
        public float BufferingProgress => 100;
        public (int Width, int Height) VideoSize => (0, 0);
        public SKBitmap? CurrentFrame => null;
        public void Dispose() { }
    }

    /// <summary>Creates a backend. It must be cheap: heavy initialisation belongs in <see cref="Preload"/> or the first <see cref="IMediaBackend.Open"/>.</summary>
    public static Func<IMediaBackend> Factory { get; private set; } = () => new Unsupported();

    /// <summary>Warms the engine up on a background thread, if it has anything to warm.</summary>
    public static Action? Preload { get; private set; }

    /// <summary>False until a platform package has called <see cref="Install"/>; players then report <see cref="MediaStatus.Error"/>.</summary>
    public static bool IsInstalled { get; private set; }

    public static void Install(Func<IMediaBackend> factory, Action? preload = null)
    {
        Factory = factory;
        Preload = preload;
        IsInstalled = true;
    }
}

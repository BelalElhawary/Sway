using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using SkiaSharp;

namespace Sway.Media;

/// <summary>
/// Plays audio and video in the browser through an HTML media element, whatever codecs the browser supports. The
/// picture is read back through a canvas, so a cross-origin URL needs CORS headers to show video (its sound plays
/// regardless). Install it once at startup, before the app runs: <c>await BrowserMediaBackend.InstallAsync();</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed partial class BrowserMediaBackend : IMediaBackend
{
    /// <summary>Loads the JavaScript bridge and makes the browser the engine behind every <see cref="MediaPlayerController"/>.</summary>
    public static async Task InstallAsync()
    {
        await JSHost.ImportAsync(Js.Module, "../_content/Sway.Media.Web/sway-media.js");
        MediaBackend.Install(() => new BrowserMediaBackend());
    }

    readonly int _id = Js.Create();
    bool _disposed, _looping, _muted;
    int _volume = 100;

    // Polling crosses into JavaScript, and a build reads several properties, so share one reading per ~8 ms.
    double[] _state = [0, 0, 0, 100, 0, 0];
    long _polledAt = long.MinValue;
    SKBitmap? _frame;

    double[] State
    {
        get
        {
            long now = Stopwatch.GetTimestamp();
            if (now - _polledAt > Stopwatch.Frequency / 120)
            {
                _state = Js.Poll(_id);
                _polledAt = now;
            }
            return _state;
        }
    }

    void Invalidate() => _polledAt = long.MinValue;

    public void Open(string source) { Js.Open(_id, source); Invalidate(); }
    public void Play() { Js.Play(_id); Invalidate(); }
    public void Pause() { Js.Pause(_id); Invalidate(); }
    public void Resume() => Play();
    public void Stop() { Js.Stop(_id); Invalidate(); }

    public TimeSpan Duration => TimeSpan.FromSeconds(State[1]);

    public TimeSpan Position
    {
        get => TimeSpan.FromSeconds(State[0]);
        set { Js.Seek(_id, value.TotalSeconds); Invalidate(); }
    }

    public int Volume
    {
        get => _volume;
        set { _volume = value; Js.SetVolume(_id, Math.Clamp(value, 0, 100) / 100.0); }
    }

    public bool Muted
    {
        get => _muted;
        set { _muted = value; Js.SetMuted(_id, value); }
    }

    public bool Looping
    {
        get => _looping;
        set { _looping = value; Js.SetLoop(_id, value); }
    }

    public MediaStatus Status => (MediaStatus)(int)State[2];

    public float BufferingProgress => (float)State[3];

    public (int Width, int Height) VideoSize => ((int)State[4], (int)State[5]);

    public SKBitmap? CurrentFrame
    {
        get
        {
            var (w, h) = VideoSize;
            if (w <= 0 || h <= 0) return null;
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
            bool resized = _frame is null || _frame.Info != info;
            if (resized)
            {
                _frame?.Dispose();
                _frame = new SKBitmap(info);
            }
            bool copied = Js.CopyFrame(_id, _frame!.GetPixelSpan());
            // A bitmap that was just allocated has no picture yet; wait for the next frame instead of drawing black.
            return copied || !resized ? _frame : null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Js.Dispose(_id);
        _frame?.Dispose();
    }
}

[SupportedOSPlatform("browser")]
static partial class Js
{
    public const string Module = "sway-media";

    [JSImport("create", Module)] internal static partial int Create();
    [JSImport("open", Module)] internal static partial void Open(int id, string source);
    [JSImport("play", Module)] internal static partial void Play(int id);
    [JSImport("pause", Module)] internal static partial void Pause(int id);
    [JSImport("stop", Module)] internal static partial void Stop(int id);
    [JSImport("seek", Module)] internal static partial void Seek(int id, double seconds);
    [JSImport("setVolume", Module)] internal static partial void SetVolume(int id, double volume);
    [JSImport("setMuted", Module)] internal static partial void SetMuted(int id, bool muted);
    [JSImport("setLoop", Module)] internal static partial void SetLoop(int id, bool loop);
    [JSImport("dispose", Module)] internal static partial void Dispose(int id);
    [JSImport("poll", Module)][return: JSMarshalAs<JSType.Array<JSType.Number>>] internal static partial double[] Poll(int id);
    [JSImport("copyFrame", Module)] internal static partial bool CopyFrame(int id, [JSMarshalAs<JSType.MemoryView>] Span<byte> destination);
}

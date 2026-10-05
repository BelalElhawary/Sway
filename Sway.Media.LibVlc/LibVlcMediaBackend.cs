using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SkiaSharp;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Sway.Media;

public enum MediaStatus { Idle, Opening, Buffering, Playing, Paused, Ended, Error }

/// <summary>
/// Plays audio or video through LibVLC. Audio goes straight to the sound device; video frames are decoded into a
/// buffer that <see cref="VideoPlayer"/> paints. Call the control methods and read the properties from the UI thread;
/// LibVLC's own threads only touch the frame buffer, which is guarded by a lock.
/// </summary>
public sealed class MediaPlayerController : IDisposable
{
    static readonly object InitLock = new();
    static LibVLC? _libVlc;

    static LibVLC LibVlc
    {
        get
        {
            lock (InitLock)
            {
                if (_libVlc is null)
                {
                    Core.Initialize();
                    _libVlc = new LibVLC("--no-video-title-show", "--quiet");
                }
                return _libVlc;
            }
        }
    }

    MediaPlayer? _player;
    int _volume = 100;
    bool _muted;
    VlcMedia? _media;
    bool _disposed;
    volatile bool _error, _ended;
    volatile float _buffering = 100;

    // Frames go through three buffers so the decoder never waits for the UI's copy (a stalled decoder makes video
    // run behind the audio): one being decoded into, the newest finished one, and one the UI is copying out of.
    readonly object _frameLock = new();
    readonly IntPtr[] _buffers = new IntPtr[3];
    int _decoding = -1, _ready = -1, _reading = -1;
    int _bufferWidth, _bufferHeight, _bufferPitch;
    bool _frameDirty;
    bool _hasFrame;
    SKBitmap? _frame;

    // LibVLC keeps only the delegates it was given as native callbacks, so hold them for the player's lifetime.
    readonly MediaPlayer.LibVLCVideoFormatCb _formatCb;
    readonly MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;
    readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    readonly MediaPlayer.LibVLCVideoUnlockCb _unlockCb;
    readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;

    public MediaPlayerController()
    {
        _formatCb = OnFormat;
        _cleanupCb = (ref IntPtr _) => { };
        _lockCb = OnLock;
        _unlockCb = (_, _, _) => { };
        _displayCb = OnDisplay;
    }

    /// <summary>
    /// Loads LibVLC on a background thread so the first <see cref="Open"/> does not stall the UI. Loading the native
    /// library and scanning its plugins takes long enough to freeze a page, so call this at startup.
    /// </summary>
    public static void Preload() => Task.Run(() => LibVlc);

    // Created on first use: building a MediaPlayer forces LibVLC to load, which is too slow for a page build.
    MediaPlayer Player
    {
        get
        {
            if (_player is not null) return _player;
            var player = new MediaPlayer(LibVlc);
            player.EndReached += (_, _) => _ended = true;
            player.EncounteredError += (_, _) => _error = true;
            player.Buffering += (_, e) => _buffering = e.Cache;
            player.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
            player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
            player.Volume = _volume;
            player.Mute = _muted;
            return _player = player;
        }
    }

    /// <summary>Raised on the calling thread after a control method changes the player (open, play, pause, seek, ...).</summary>
    public event Action? Changed;

    // Native calls (and loading LibVLC itself, which takes seconds on a cold start) run here, in order, so the UI
    // thread never waits on them. Reads stay on the UI thread; they are cheap.
    readonly object _queueLock = new();
    Task _queue = Task.CompletedTask;
    volatile bool _opening;

    void Post(Action work)
    {
        lock (_queueLock)
            _queue = _queue.ContinueWith(_ =>
            {
                if (_disposed) return;
                try { work(); }
                catch { _error = true; }
            }, TaskScheduler.Default);
    }

    /// <summary>Opens a file path or a URL (http, rtsp, ...). Playback starts only after <see cref="Play"/>.</summary>
    public void Open(string source)
    {
        _error = _ended = false;
        _buffering = 100;
        _opening = true;
        Post(() =>
        {
            var player = Player;
            var old = _media;
            _media = source.Contains("://", StringComparison.Ordinal)
                ? new VlcMedia(LibVlc, new Uri(source))
                : new VlcMedia(LibVlc, source, FromType.FromPath);
            player.Media = _media;
            old?.Dispose();
            _opening = false;
        });
        Changed?.Invoke();
    }

    public void Play()
    {
        Post(() =>
        {
            if (_player?.Media is null) return;
            if (_ended) { _ended = false; _player.Stop(); }
            _player.Play();
        });
        Changed?.Invoke();
    }

    public void Pause() { Post(() => _player?.SetPause(true)); Changed?.Invoke(); }
    public void Resume() { Post(() => _player?.SetPause(false)); Changed?.Invoke(); }

    public void TogglePlay()
    {
        if (Status is MediaStatus.Playing or MediaStatus.Buffering) Pause();
        else if (Status == MediaStatus.Paused) Resume();
        else Play();
    }

    public void Stop() { _ended = false; Post(() => _player?.Stop()); Changed?.Invoke(); }

    public TimeSpan Duration => _player is { Length: > 0 } p ? TimeSpan.FromMilliseconds(p.Length) : TimeSpan.Zero;

    public TimeSpan Position
    {
        get => TimeSpan.FromMilliseconds(Math.Max(0, _player?.Time ?? 0));
        set
        {
            Post(() =>
            {
                if (_player is { } p) p.Time = (long)Math.Clamp(value.TotalMilliseconds, 0, Math.Max(0, p.Length));
            });
            Changed?.Invoke();
        }
    }

    /// <summary>0 to 100; values above 100 amplify.</summary>
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 200);
            if (_player is not null) _player.Volume = _volume;
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (_player is not null) _player.Mute = value;
        }
    }

    public MediaStatus Status
    {
        get
        {
            if (_error) return MediaStatus.Error;
            if (_opening) return MediaStatus.Opening;
            if (_ended) return MediaStatus.Ended;
            return _player?.State switch
            {
                VLCState.Opening => MediaStatus.Opening,
                VLCState.Buffering => MediaStatus.Buffering,
                VLCState.Playing => _buffering < 100 ? MediaStatus.Buffering : MediaStatus.Playing,
                VLCState.Paused => MediaStatus.Paused,
                VLCState.Ended => MediaStatus.Ended,
                VLCState.Error => MediaStatus.Error,
                _ => MediaStatus.Idle,
            };
        }
    }

    /// <summary>Cache fill, 0 to 100, while the status is <see cref="MediaStatus.Buffering"/>.</summary>
    public float BufferingProgress => _buffering;

    /// <summary>True while the player is working and the UI should keep asking for frames.</summary>
    public bool IsActive => Status is MediaStatus.Opening or MediaStatus.Buffering or MediaStatus.Playing;

    /// <summary>The decoded video size, or zero for audio-only media or before the first frame.</summary>
    public (int Width, int Height) VideoSize
    {
        get { lock (_frameLock) return (_bufferWidth, _bufferHeight); }
    }

    /// <summary>
    /// The most recent video frame, or null if there is none. The bitmap is owned by the controller and replaced
    /// when a newer frame arrives, so paint it right away and do not keep it.
    /// </summary>
    public SKBitmap? CurrentFrame
    {
        get
        {
            IntPtr source;
            int width, height, pitch;
            lock (_frameLock)
            {
                if (!_hasFrame) return null;
                if (!_frameDirty) return _frame;
                _frameDirty = false;
                _reading = _ready;
                source = _buffers[_reading];
                (width, height, pitch) = (_bufferWidth, _bufferHeight, _bufferPitch);
            }

            // Copied outside the lock; the decoder cannot pick the buffer being read.
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            if (_frame is null || _frame.Info != info)
            {
                _frame?.Dispose();
                _frame = new SKBitmap(info);
            }
            // The decoder's pitch can be wider than the row, so copy row by row.
            int rowBytes = width * 4;
            unsafe
            {
                for (int y = 0; y < height; y++)
                    Buffer.MemoryCopy((byte*)source + (long)y * pitch, (byte*)_frame.GetPixels() + (long)y * _frame.RowBytes, _frame.RowBytes, rowBytes);
            }
            lock (_frameLock) _reading = -1;
            return _frame;
        }
    }

    uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4); // BGRA, matching SKColorType.Bgra8888
        while (true)
        {
            lock (_frameLock)
            {
                if (_reading == -1)
                {
                    FreeBuffers();
                    _bufferWidth = (int)width;
                    _bufferHeight = (int)height;
                    _bufferPitch = (int)width * 4;
                    pitches = (uint)_bufferPitch;
                    lines = height;
                    for (int i = 0; i < _buffers.Length; i++) _buffers[i] = Marshal.AllocHGlobal(_bufferPitch * _bufferHeight);
                    return 1;
                }
            }
            Thread.Sleep(1); // the UI is mid-copy from the old buffers
        }
    }

    void FreeBuffers()
    {
        for (int i = 0; i < _buffers.Length; i++)
        {
            if (_buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_buffers[i]);
            _buffers[i] = IntPtr.Zero;
        }
        _decoding = _ready = -1;
        _hasFrame = _frameDirty = false;
    }

    // Decodes into a buffer that is neither the newest finished frame nor the one the UI is reading.
    IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        lock (_frameLock)
        {
            int next = 0;
            while (next == _ready || next == _reading) next++;
            _decoding = next;
            Marshal.WriteIntPtr(planes, _buffers[next]);
        }
        return IntPtr.Zero;
    }

    void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        lock (_frameLock)
        {
            _ready = _decoding;
            _frameDirty = _hasFrame = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Behind any queued work, which is skipped now that _disposed is set.
        lock (_queueLock)
            _queue = _queue.ContinueWith(_ =>
            {
                _player?.Stop();
                _player?.Dispose();
                _media?.Dispose();
                lock (_frameLock)
                {
                    _frame?.Dispose();
                    FreeBuffers();
                }
            }, TaskScheduler.Default);
    }
}

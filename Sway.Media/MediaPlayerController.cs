using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SkiaSharp;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace Sway.Media;

public enum MediaStatus { Idle, Opening, Playing, Paused, Ended, Error }

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

    readonly MediaPlayer _player;
    VlcMedia? _media;
    bool _disposed;
    volatile bool _error, _ended;

    // Frame buffer written by LibVLC's decoder thread.
    readonly object _frameLock = new();
    IntPtr _buffer;
    int _bufferWidth, _bufferHeight, _bufferPitch;
    bool _frameDirty;
    SKBitmap? _frame;

    // LibVLC keeps only the delegates it was given as native callbacks, so hold them for the player's lifetime.
    readonly MediaPlayer.LibVLCVideoFormatCb _formatCb;
    readonly MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;
    readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    readonly MediaPlayer.LibVLCVideoUnlockCb _unlockCb;
    readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;

    public MediaPlayerController()
    {
        _player = new MediaPlayer(LibVlc);
        _player.EndReached += (_, _) => _ended = true;
        _player.EncounteredError += (_, _) => _error = true;

        _formatCb = OnFormat;
        _cleanupCb = (ref IntPtr _) => { };
        _lockCb = OnLock;
        _unlockCb = (_, _, _) => Monitor.Exit(_frameLock);
        _displayCb = (_, _) => { _frameDirty = true; };
        _player.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
        _player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
    }

    /// <summary>Raised on the calling thread after a control method changes the player (open, play, pause, seek, ...).</summary>
    public event Action? Changed;

    /// <summary>Opens a file path or a URL (http, rtsp, ...). Playback starts only after <see cref="Play"/>.</summary>
    public void Open(string source)
    {
        _media?.Dispose();
        _error = _ended = false;
        _media = source.Contains("://", StringComparison.Ordinal)
            ? new VlcMedia(LibVlc, new Uri(source))
            : new VlcMedia(LibVlc, source, FromType.FromPath);
        _player.Media = _media;
        Changed?.Invoke();
    }

    public void Play()
    {
        if (_player.Media is null) return;
        if (_ended) { _ended = false; _player.Stop(); }
        _player.Play();
        Changed?.Invoke();
    }

    public void Pause() { _player.SetPause(true); Changed?.Invoke(); }
    public void Resume() { _player.SetPause(false); Changed?.Invoke(); }

    public void TogglePlay()
    {
        if (Status == MediaStatus.Playing) Pause();
        else if (Status == MediaStatus.Paused) Resume();
        else Play();
    }

    public void Stop() { _ended = false; _player.Stop(); Changed?.Invoke(); }

    public TimeSpan Duration => _player.Length > 0 ? TimeSpan.FromMilliseconds(_player.Length) : TimeSpan.Zero;

    public TimeSpan Position
    {
        get => TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
        set { _player.Time = (long)Math.Clamp(value.TotalMilliseconds, 0, Math.Max(0, _player.Length)); Changed?.Invoke(); }
    }

    /// <summary>0 to 100; values above 100 amplify.</summary>
    public int Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0, 200);
    }

    public bool Muted
    {
        get => _player.Mute;
        set => _player.Mute = value;
    }

    public MediaStatus Status
    {
        get
        {
            if (_error) return MediaStatus.Error;
            if (_ended) return MediaStatus.Ended;
            return _player.State switch
            {
                VLCState.Opening or VLCState.Buffering => MediaStatus.Opening,
                VLCState.Playing => MediaStatus.Playing,
                VLCState.Paused => MediaStatus.Paused,
                VLCState.Ended => MediaStatus.Ended,
                VLCState.Error => MediaStatus.Error,
                _ => MediaStatus.Idle,
            };
        }
    }

    /// <summary>True while the player is working and the UI should keep asking for frames.</summary>
    public bool IsActive => Status is MediaStatus.Opening or MediaStatus.Playing;

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
            lock (_frameLock)
            {
                if (_buffer == IntPtr.Zero) return null;
                if (_frameDirty || _frame is null)
                {
                    _frameDirty = false;
                    var info = new SKImageInfo(_bufferWidth, _bufferHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
                    if (_frame is null || _frame.Info != info)
                    {
                        _frame?.Dispose();
                        _frame = new SKBitmap(info);
                    }
                    // The decoder's pitch can be wider than the row, so copy row by row.
                    int rowBytes = _bufferWidth * 4;
                    for (int y = 0; y < _bufferHeight; y++)
                    {
                        unsafe
                        {
                            Buffer.MemoryCopy((byte*)_buffer + (long)y * _bufferPitch, (byte*)_frame.GetPixels() + (long)y * _frame.RowBytes,
                                _frame.RowBytes, rowBytes);
                        }
                    }
                }
                return _frame;
            }
        }
    }

    uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4); // BGRA, matching SKColorType.Bgra8888
        lock (_frameLock)
        {
            if (_buffer != IntPtr.Zero) Marshal.FreeHGlobal(_buffer);
            _bufferWidth = (int)width;
            _bufferHeight = (int)height;
            _bufferPitch = (int)width * 4;
            pitches = (uint)_bufferPitch;
            lines = height;
            _buffer = Marshal.AllocHGlobal(_bufferPitch * _bufferHeight);
        }
        return 1;
    }

    // Held until the matching unlock so the UI thread never reads a half-written frame.
    IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        Monitor.Enter(_frameLock);
        Marshal.WriteIntPtr(planes, _buffer);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.Stop();
        _player.Dispose();
        _media?.Dispose();
        lock (_frameLock)
        {
            _frame?.Dispose();
            if (_buffer != IntPtr.Zero) Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }
}

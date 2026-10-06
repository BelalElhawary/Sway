using System.Reflection;
using SkiaSharp;

namespace Sway.Widgets;

/// <summary>How an image is fitted into the box it is painted in.</summary>
public enum BoxFit { Fill, Contain, Cover, FitWidth, FitHeight, None, ScaleDown }

/// <summary>
/// Where an <see cref="Image"/> comes from. Sources are compared by <see cref="Key"/>, so rebuilding with an equal source does not reload.
/// Decoded images from files, URLs and resources are cached by key (the cache holds the 128 most recently added).
/// </summary>
public abstract class ImageSource
{
    static readonly HttpClient Http = new();
    static readonly object Gate = new();
    static readonly Dictionary<string, SKImage> Cache = new();
    static readonly Queue<string> Order = new();
    const int CacheLimit = 128;

    /// <summary>Identifies the source for caching and change detection.</summary>
    public abstract string Key { get; }

    /// <summary>Whether <see cref="Key"/> can be used to cache the decoded image. False for sources that already hold their pixels.</summary>
    protected virtual bool Cacheable => true;

    protected abstract Task<byte[]?> ReadAsync(CancellationToken cancel);

    /// <summary>Loads and decodes the image off the UI thread. Returns null when it can't be read or isn't an image.</summary>
    public async Task<SKImage?> LoadAsync(CancellationToken cancel = default)
    {
        if (Cacheable)
            lock (Gate) if (Cache.TryGetValue(Key, out var hit)) return hit;
        try
        {
            var bytes = await ReadAsync(cancel).ConfigureAwait(false);
            if (bytes is null) return null;
            var image = await Task.Run(() => SKImage.FromEncodedData(bytes), cancel).ConfigureAwait(false);
            if (image is not null && Cacheable)
                lock (Gate)
                {
                    if (Cache.TryAdd(Key, image)) Order.Enqueue(Key);
                    while (Order.Count > CacheLimit) Cache.Remove(Order.Dequeue());
                }
            return image;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    /// <summary>The image already decoded in memory, such as one drawn with Skia. Shown immediately, with no loading state.</summary>
    public static ImageSource FromImage(SKImage image) => new ImageImageSource(image);
    /// <summary>Encoded bytes (PNG, JPEG, WebP...).</summary>
    public static ImageSource FromBytes(byte[] bytes, string? key = null) => new BytesImageSource(bytes, key);
    public static ImageSource FromFile(string path) => new FileImageSource(path);
    /// <summary>An http(s) URL, fetched with a shared <see cref="HttpClient"/>.</summary>
    public static ImageSource FromUrl(string url) => new UrlImageSource(url);
    /// <summary>A resource embedded in <paramref name="assembly"/>, by its manifest resource name.</summary>
    public static ImageSource FromResource(Assembly assembly, string name) => new ResourceImageSource(assembly, name);

    sealed class ImageImageSource(SKImage image) : ImageSource
    {
        public SKImage Image => image;
        public override string Key => "image:" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(image);
        protected override bool Cacheable => false;
        protected override Task<byte[]?> ReadAsync(CancellationToken cancel) => Task.FromResult<byte[]?>(null);
    }

    sealed class BytesImageSource(byte[] bytes, string? key) : ImageSource
    {
        public override string Key => key ?? "bytes:" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(bytes);
        protected override bool Cacheable => key is not null;
        protected override Task<byte[]?> ReadAsync(CancellationToken cancel) => Task.FromResult<byte[]?>(bytes);
    }

    sealed class FileImageSource(string path) : ImageSource
    {
        public override string Key => "file:" + path;
        protected override async Task<byte[]?> ReadAsync(CancellationToken cancel) => await File.ReadAllBytesAsync(path, cancel).ConfigureAwait(false);
    }

    sealed class UrlImageSource(string url) : ImageSource
    {
        public override string Key => "url:" + url;
        protected override async Task<byte[]?> ReadAsync(CancellationToken cancel) => await Http.GetByteArrayAsync(url, cancel).ConfigureAwait(false);
    }

    sealed class ResourceImageSource(Assembly assembly, string name) : ImageSource
    {
        public override string Key => $"resource:{assembly.GetName().Name}/{name}";
        protected override async Task<byte[]?> ReadAsync(CancellationToken cancel)
        {
            await using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) return null;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancel).ConfigureAwait(false);
            return ms.ToArray();
        }
    }

    /// <summary>The pixels of an in-memory source, so <see cref="Image"/> can show it without waiting a frame.</summary>
    internal SKImage? Immediate => (this as ImageImageSource)?.Image;
}

/// <summary>
/// Shows an <see cref="ImageSource"/>, loading it in the background. While it loads, <paramref name="placeholder"/> (or nothing) is shown;
/// if it can't be read or decoded, <paramref name="error"/> is shown instead. It fills the box it is given, fitted by <paramref name="fit"/>;
/// with loose constraints and no width or height it takes the image's own size.
/// </summary>
public sealed class Image(ImageSource source, BoxFit fit = BoxFit.Cover, IAlignment? alignment = null, float? width = null, float? height = null,
    Widget? placeholder = null, Widget? error = null, Key? key = null) : StatefulWidget(key)
{
    internal ImageSource Source => source;
    internal BoxFit Fit => fit;
    internal IAlignment? ImageAlignment => alignment;
    internal float? W => width;
    internal float? H => height;
    internal Widget? Placeholder => placeholder;
    internal Widget? Error => error;
    public override State CreateState() => new ImageState();
}

sealed class ImageState : State<Image>
{
    SKImage? _image;
    bool _failed;
    CancellationTokenSource? _cancel;

    public override void InitState() => Load();

    public override void DidUpdateWidget(Image old)
    {
        if (old.Source.Key != Widget.Source.Key) Load();
    }

    public override void Dispose() { _cancel?.Cancel(); _cancel = null; }

    void Load()
    {
        _cancel?.Cancel();
        _cancel = null;
        _failed = false;
        if (Widget.Source.Immediate is { } now) { _image = now; return; }
        _image = null;
        var cts = _cancel = new CancellationTokenSource();
        var binding = WidgetsBinding.Instance;
        _ = Widget.Source.LoadAsync(cts.Token).ContinueWith(t =>
        {
            if (cts.IsCancellationRequested) return;
            var result = t.IsCompletedSuccessfully ? t.Result : null;
            binding.Post(() =>
            {
                if (cts.IsCancellationRequested || !Mounted) return;
                SetState(() => { _image = result; _failed = result is null; });
            });
        }, TaskScheduler.Default);
    }

    public override Widget Build(BuildContext context)
    {
        if (_image is null)
        {
            Widget? fallback = _failed ? Widget.Error ?? Widget.Placeholder : Widget.Placeholder;
            return new SizedBox(Widget.W, Widget.H, fallback ?? new SizedBox());
        }
        var painter = new ImagePainter(_image, Widget.Fit, (Widget.ImageAlignment ?? Alignment.Center).Resolve(Directionality.Of(context)));
        Size preferred = Widget.W is not null || Widget.H is not null
            ? new Size(Widget.W ?? 0, Widget.H ?? 0)
            : new Size(_image.Width, _image.Height);
        return new ClipRect(new CustomPaint(painter, size: preferred));
    }
}

sealed class ImagePainter(SKImage image, BoxFit fit, Alignment alignment) : CustomPainter
{
    static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public override void Paint(SKCanvas canvas, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        float iw = image.Width, ih = image.Height;
        // The scale applied to the image to reach its painted size.
        float sx = size.Width / iw, sy = size.Height / ih;
        (float w, float h) = fit switch
        {
            BoxFit.Fill => (size.Width, size.Height),
            BoxFit.Contain => Scaled(MathF.Min(sx, sy)),
            BoxFit.Cover => Scaled(MathF.Max(sx, sy)),
            BoxFit.FitWidth => Scaled(sx),
            BoxFit.FitHeight => Scaled(sy),
            BoxFit.ScaleDown => Scaled(MathF.Min(1, MathF.Min(sx, sy))),
            _ => (iw, ih),
        };
        (float w, float h) Scaled(float s) => (iw * s, ih * s);

        float left = (size.Width - w) / 2 * (1 + alignment.X), top = (size.Height - h) / 2 * (1 + alignment.Y);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));
        canvas.DrawImage(image, new SKRect(0, 0, iw, ih), new SKRect(left, top, left + w, top + h), Sampling, paint);
        canvas.Restore();
    }

    public override bool ShouldRepaint(CustomPainter old) =>
        old is not ImagePainter p || !ReferenceEquals(p.Img, image) || p.Fit != fit || p.Align != alignment;

    SKImage Img => image;
    BoxFit Fit => fit;
    Alignment Align => alignment;
}

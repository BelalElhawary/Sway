using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class ImageTests
{
    static SKImage Solid(SKColor color, int w, int h)
    {
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(color);
        return SKImage.FromBitmap(bmp);
    }

    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255);

    static Harness Show(Widget image, int size = 100) =>
        new(new ColoredBox(SKColors.White, new Align(Alignment.TopLeft, new SizedBox(size, size, image))), size, size);

    static SKColor Pixel(Harness h, int x, int y)
    {
        using var bmp = h.Render();
        return bmp.GetPixel(x, y);
    }

    // Loading runs on the thread pool and lands on the next frame, so the harness pumps until the picture shows up.
    static bool PumpUntil(Harness h, Func<bool> done)
    {
        for (int i = 0; i < 150 && !done(); i++) { Thread.Sleep(20); h.Pump(); }
        return done();
    }

    [Fact]
    public void AnInMemoryImageShowsImmediately()
    {
        var h = Show(new Image(ImageSource.FromImage(Solid(Red, 40, 20))));
        Assert.Equal(Red, Pixel(h, 50, 50));
    }

    [Fact]
    public void CoverFillsTheBoxAndContainLeavesBars()
    {
        var cover = Show(new Image(ImageSource.FromImage(Solid(Red, 40, 20)), BoxFit.Cover));
        Assert.Equal(Red, Pixel(cover, 2, 2));
        Assert.Equal(Red, Pixel(cover, 97, 97));

        var contain = Show(new Image(ImageSource.FromImage(Solid(Red, 40, 20)), BoxFit.Contain));
        Assert.Equal(Red, Pixel(contain, 50, 50));
        Assert.Equal(SKColors.White, Pixel(contain, 50, 5));
    }

    [Fact]
    public void AlignmentPicksTheEdgeContainHugs()
    {
        var h = Show(new Image(ImageSource.FromImage(Solid(Red, 40, 20)), BoxFit.Contain, Alignment.TopCenter));
        Assert.Equal(Red, Pixel(h, 50, 5));
        Assert.Equal(SKColors.White, Pixel(h, 50, 95));
    }

    [Fact]
    public void EncodedBytesLoadInTheBackground()
    {
        using var data = Solid(Blue, 10, 10).Encode(SKEncodedImageFormat.Png, 100);
        var h = Show(new Image(ImageSource.FromBytes(data.ToArray())));
        Assert.True(PumpUntil(h, () => Pixel(h, 50, 50) == Blue));
    }

    [Fact]
    public void AFileLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sway-image-{Guid.NewGuid():N}.png");
        using (var data = Solid(Blue, 10, 10).Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(path, data.ToArray());
        try
        {
            var h = Show(new Image(ImageSource.FromFile(path)));
            Assert.True(PumpUntil(h, () => Pixel(h, 50, 50) == Blue));
        }
        finally { File.Delete(path); }
    }

    sealed class Gated(string key) : ImageSource
    {
        public readonly TaskCompletionSource<byte[]?> Gate = new();
        public override string Key => key;
        protected override bool Cacheable => false;
        protected override Task<byte[]?> ReadAsync(CancellationToken cancel) => Gate.Task;
    }

    [Fact]
    public void ThePlaceholderShowsWhileLoadingThenTheImage()
    {
        var src = new Gated("gate-1");
        var h = Show(new Image(src, placeholder: new ColoredBox(Red)));
        Assert.Equal(Red, Pixel(h, 50, 50));
        using var data = Solid(Blue, 10, 10).Encode(SKEncodedImageFormat.Png, 100);
        src.Gate.SetResult(data.ToArray());
        Assert.True(PumpUntil(h, () => Pixel(h, 50, 50) == Blue));
    }

    [Fact]
    public void ABrokenImageShowsTheErrorWidget()
    {
        var h = Show(new Image(ImageSource.FromBytes([1, 2, 3]), placeholder: new ColoredBox(Red), error: new ColoredBox(Blue)));
        Assert.True(PumpUntil(h, () => Pixel(h, 50, 50) == Blue));
    }

    [Fact]
    public void AMissingFileShowsTheErrorWidget()
    {
        var h = Show(new Image(ImageSource.FromFile(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".png")), error: new ColoredBox(Blue)));
        Assert.True(PumpUntil(h, () => Pixel(h, 50, 50) == Blue));
    }

    [Fact]
    public void ChangingTheSourceReloads()
    {
        var h = new Harness(new Host(Solid(Red, 10, 10), Solid(Blue, 10, 10)), 100, 100);
        Assert.Equal(Red, Pixel(h, 50, 50));
        h.Tap(50, 50);
        Assert.Equal(Blue, Pixel(h, 50, 50));
    }

    sealed class Host(SKImage a, SKImage b) : StatefulWidget
    {
        internal SKImage A => a;
        internal SKImage B => b;
        public override State CreateState() => new HostState();
    }

    sealed class HostState : State<Host>
    {
        bool _second;
        public override Widget Build(BuildContext context) => new GestureDetector(onTap: () => SetState(() => _second = !_second),
            behavior: HitTestBehavior.Opaque, child: new Image(ImageSource.FromImage(_second ? Widget.B : Widget.A)));
    }
}

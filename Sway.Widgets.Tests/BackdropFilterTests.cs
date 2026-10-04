using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class BackdropFilterTests
{
    static readonly SKColor Red = new(255, 0, 0), Blue = new(0, 0, 255);

    // Red on the left half, blue on the right half of a 400x300 window; the filter covers x 100..300, y 100..200.
    static Widget Scene(float blur, SKColorFilter? colorFilter = null, Widget? above = null) => new Stack(
    [
        new Row([new Expanded(new ColoredBox(Red)), new Expanded(new ColoredBox(Blue))], crossAxisAlignment: CrossAxisAlignment.Stretch),
        new Positioned(new BackdropFilter(above, blur, colorFilter), left: 100, top: 100, width: 200, height: 100),
    ], fit: StackFit.Expand);

    static SKColor PixelAt(Harness h, int x, int y)
    {
        using var bitmap = h.Render();
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public void BlurMixesWhatIsBehindItAcrossAnEdge()
    {
        var h = new Harness(Scene(blur: 10));
        var inside = PixelAt(h, 196, 150);
        Assert.InRange((int)inside.Red, 60, 220);
        Assert.InRange((int)inside.Blue, 60, 220);
    }

    [Fact]
    public void ContentOutsideTheFilterIsUntouched()
    {
        var h = new Harness(Scene(blur: 10));
        Assert.Equal(Red, PixelAt(h, 196, 50));
        Assert.Equal(Blue, PixelAt(h, 204, 250));
        Assert.Equal(Red, PixelAt(h, 90, 150));
    }

    [Fact]
    public void FlatAreasStayTheSameColourWhenBlurred()
    {
        var h = new Harness(Scene(blur: 10));
        var flat = PixelAt(h, 120, 150);
        Assert.InRange((int)flat.Red, 235, 255);
        Assert.InRange((int)flat.Blue, 0, 20);
    }

    [Fact]
    public void TheChildPaintsOnTopOfTheFilteredBackdrop()
    {
        var h = new Harness(Scene(blur: 10, above: new ColoredBox(new SKColor(0, 255, 0, 255), new SizedBox(50, 50))));
        Assert.Equal(new SKColor(0, 255, 0, 255), PixelAt(h, 120, 110));
    }

    [Fact]
    public void ColourFilterRecoloursTheBackdrop()
    {
        var h = new Harness(Scene(blur: 0, colorFilter: ColorFilters.Grayscale()));
        var inside = PixelAt(h, 120, 150);
        Assert.Equal(inside.Red, inside.Green);
        Assert.Equal(inside.Green, inside.Blue);
        Assert.Equal(Red, PixelAt(h, 120, 50));
    }

    [Fact]
    public void ImageFilteredBlursOnlyItsOwnChild()
    {
        // Unlike a backdrop filter, ImageFiltered never touches what is behind it.
        var h = new Harness(new Stack(
        [
            new Row([new Expanded(new ColoredBox(Red)), new Expanded(new ColoredBox(Blue))], crossAxisAlignment: CrossAxisAlignment.Stretch),
            new Positioned(new ImageFiltered(new SizedBox(), blur: 10), left: 100, top: 100, width: 200, height: 100),
        ], fit: StackFit.Expand));
        Assert.Equal(Red, PixelAt(h, 196, 150));
    }
}

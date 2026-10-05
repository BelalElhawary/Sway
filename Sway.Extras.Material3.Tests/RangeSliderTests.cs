using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class RangeSliderTests
{
    // The track runs from x=10 to x=210 (thumb radius 10 on each side), so a fraction f sits at x = 10 + 200 * f.
    static float X(float fraction) => 10 + 200 * fraction;

    static (Harness harness, Func<RangeValues> values) Build(RangeValues initial, int? divisions = null, Action<RangeValues>? onEnd = null)
    {
        var value = initial;
        var host = new TestHost(h => Top(new SizedBox(width: 220, child: new RangeSlider(value, v => { value = v; h.Refresh!(); },
            divisions: divisions, onChangeEnd: onEnd))));
        return (new Harness(host), () => value);
    }

    [Fact]
    public void TapMovesTheNearerThumb()
    {
        var (h, values) = Build(new RangeValues(0.2f, 0.8f));
        h.Tap(X(0.9f), 20);
        Assert.Equal(0.2f, values().Start, 2);
        Assert.Equal(0.9f, values().End, 2);

        h.Tap(X(0.1f), 20);
        Assert.Equal(0.1f, values().Start, 2);
        Assert.Equal(0.9f, values().End, 2);
    }

    [Fact]
    public void DragMovesTheGrabbedThumbAndStopsAtTheOtherOne()
    {
        RangeValues? ended = null;
        var (h, values) = Build(new RangeValues(0.2f, 0.6f), onEnd: v => ended = v);
        h.Gestures.PointerDown(X(0.2f), 20);
        h.Gestures.PointerMove(X(0.3f), 20);
        h.Gestures.PointerMove(X(0.4f), 20);
        Assert.Equal(0.4f, values().Start, 2);
        h.Gestures.PointerMove(X(0.95f), 20);
        Assert.Equal(0.6f, values().Start, 2); // cannot pass the end thumb
        Assert.Equal(0.6f, values().End, 2);
        h.Gestures.PointerUp(X(0.95f), 20);
        Assert.Equal(0.6f, ended!.Value.Start, 2);
    }

    [Fact]
    public void ValuesSnapToDivisions()
    {
        var (h, values) = Build(new RangeValues(0f, 1f), divisions: 4);
        h.Tap(X(0.68f), 20);
        Assert.Equal(0.75f, values().End, 3);
    }

    [Fact]
    public void ArrowKeysMoveTheActiveThumb()
    {
        var (h, values) = Build(new RangeValues(0.2f, 0.8f), divisions: 10);
        h.Tap(X(0.8f), 20); // grabs the end thumb
        h.Binding.Focus.Traverse(1);
        h.Binding.KeyDown("ArrowLeft", "ArrowLeft");
        Assert.Equal(0.7f, values().End, 2);
    }
}

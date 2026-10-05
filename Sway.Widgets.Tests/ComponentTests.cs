using Xunit;

namespace Sway.Widgets.Tests;

public class ComponentTests
{
    // A stateful host so controls can rebuild with their new value, like an app would.
    sealed class Host(Func<Host, Widget> build) : StatefulWidget
    {
        public override State CreateState() => new HostState();
        internal Func<Host, Widget> Build => build;
        public Action? Refresh;
    }

    sealed class HostState : State<Host>
    {
        public override void InitState() => Widget.Refresh = () => SetState();
        public override Widget Build(BuildContext context) => Widget.Build(Widget);
    }

    static Widget Top(Widget child) => new Align(Alignment.TopLeft, child);

    // ---- Slider ----

    [Fact]
    public void SliderTapSetsTheValueUnderThePointer()
    {
        float value = 0;
        var host = new Host(h => Top(new SizedBox(width: 220, child: new Slider(value, v => { value = v; h.Refresh!(); }))));
        var h = new Harness(host);
        // Track runs from x=10 to x=210 (thumb radius 10 on each side): x=110 is the middle.
        h.Gestures.PointerDown(110, 20);
        h.Gestures.PointerUp(110, 20);
        Assert.Equal(0.5f, value, 2);
    }

    [Fact]
    public void SliderDragFollowsThePointerAndClamps()
    {
        float value = 0, ended = -1;
        var host = new Host(h => Top(new SizedBox(width: 220, child: new Slider(value, v => { value = v; h.Refresh!(); }, onChangeEnd: v => ended = v))));
        var h = new Harness(host);
        h.Gestures.PointerDown(10, 20);
        h.Gestures.PointerMove(60, 20);
        h.Gestures.PointerMove(110, 20);
        Assert.Equal(0.5f, value, 2);
        h.Gestures.PointerMove(900, 20);
        Assert.Equal(1f, value, 3);
        h.Gestures.PointerUp(900, 20);
        Assert.Equal(1f, ended, 3);
    }

    [Fact]
    public void SliderSnapsToDivisions()
    {
        float value = 0;
        var host = new Host(h => Top(new SizedBox(width: 220, child: new Slider(value, v => { value = v; h.Refresh!(); }, min: 0, max: 100, divisions: 4))));
        var h = new Harness(host);
        h.Tap(10 + 200 * 0.68f, 20);
        Assert.Equal(75f, value);
    }

    [Fact]
    public void SliderKeyboardMovesTheValue()
    {
        float value = 0.5f;
        var host = new Host(h => Top(new SizedBox(width: 220, child: new Slider(value, v => { value = v; h.Refresh!(); }))));
        var h = new Harness(host);
        h.Binding.Focus.Traverse(1);
        h.Binding.KeyDown("ArrowRight", "ArrowRight");
        Assert.Equal(0.55f, value, 3);
        h.Binding.KeyDown("Home", "Home");
        Assert.Equal(0f, value);
        h.Binding.KeyDown("End", "End");
        Assert.Equal(1f, value);
        h.Binding.KeyDown("PageDown", "PageDown");
        Assert.Equal(0.9f, value, 3);
    }

    [Fact]
    public void SliderRightToLeftMirrorsTheTrack()
    {
        float value = 0;
        var host = new Host(h => Top(new Directionality(TextDirection.Rtl,
            new SizedBox(width: 220, child: new Slider(value, v => { value = v; h.Refresh!(); })))));
        var h = new Harness(host);
        // The minimum sits at the right end in RTL.
        h.Tap(200, 20);
        Assert.Equal(0.05f, value, 2);
    }

    [Fact]
    public void DisabledSliderIgnoresInput()
    {
        var h = new Harness(Top(new SizedBox(width: 220, child: new Slider(0.2f, onChanged: null))));
        h.Tap(200, 20);
        h.Binding.Focus.Traverse(1);
        Assert.Null(h.Binding.Focus.Primary);
    }

    // ---- SegmentedButton ----

    [Fact]
    public void SingleSelectionReplacesTheSelection()
    {
        IReadOnlyCollection<string> selected = new[] { "a" };
        var host = new Host(h => Top(new SegmentedButton<string>(
            [new ButtonSegment<string>("a", "A"), new ButtonSegment<string>("b", "B"), new ButtonSegment<string>("c", "C")],
            selected, v => { selected = v; h.Refresh!(); })));
        var h = new Harness(host);
        var boxes = h.Find<RenderMouseRegion>();
        var second = boxes[1].LocalToGlobal(Offset.Zero);
        h.Tap(second.Dx + 5, second.Dy + 5);
        Assert.Equal(new[] { "b" }, selected.ToArray());
    }

    [Fact]
    public void MultiSelectionToggles()
    {
        IReadOnlyCollection<string> selected = new[] { "a" };
        var host = new Host(h => Top(new SegmentedButton<string>(
            [new ButtonSegment<string>("a", "A"), new ButtonSegment<string>("b", "B")],
            selected, v => { selected = v; h.Refresh!(); }, multiSelectionEnabled: true)));
        var h = new Harness(host);
        var boxes = h.Find<RenderMouseRegion>();
        var b = boxes[1].LocalToGlobal(Offset.Zero);
        h.Tap(b.Dx + 5, b.Dy + 5);
        Assert.Equal(new[] { "a", "b" }, selected.OrderBy(x => x).ToArray());
        var a = boxes[0].LocalToGlobal(Offset.Zero);
        h.Tap(a.Dx + 5, a.Dy + 5);
        Assert.Equal(new[] { "b" }, selected.ToArray());
    }

    // ---- Tabs ----

    [Fact]
    public void TabBarReportsTheTappedTab()
    {
        int tapped = -1;
        var h = new Harness(Top(new SizedBox(width: 300, child: new TabBar([new Tab("One"), new Tab("Two"), new Tab("Three")], 0, i => tapped = i))));
        h.Tap(150, 20);
        Assert.Equal(1, tapped);
        h.Tap(290, 20);
        Assert.Equal(2, tapped);
    }

    [Fact]
    public void TabBarViewShowsTheSelectedChild()
    {
        var h = new Harness(new TabBarView(1, [new Text("first"), new Text("second")]));
        h.Advance(500);
        Assert.Equal(new[] { "second" }, h.Find<RenderParagraph>().Select(p => p.PlainText).ToArray());
    }

    // ---- Badge ----

    [Fact]
    public void BadgeShowsItsLabelAndCanBeHidden()
    {
        var shown = new Harness(Top(new Badge(new SizedBox(40, 40), label: "7")));
        Assert.Contains(shown.Find<RenderParagraph>(), p => p.PlainText == "7");
        var hidden = new Harness(Top(new Badge(new SizedBox(40, 40), label: "7", isLabelVisible: false)));
        Assert.Empty(hidden.Find<RenderParagraph>());
    }

    // ---- Tooltip ----

    [Fact]
    public void TooltipAppearsAfterTheWaitAndHidesOnExit()
    {
        var h = new Harness(Top(new Tooltip("Hello tip", new SizedBox(100, 40, new ColoredBox(Colors.Red)))));
        h.Gestures.PointerMove(50, 20);
        h.Pump();
        h.Advance(300);
        Assert.DoesNotContain(h.Find<RenderParagraph>(), p => p.PlainText == "Hello tip");
        h.Advance(400);
        Assert.Contains(h.Find<RenderParagraph>(), p => p.PlainText == "Hello tip");
        h.Gestures.PointerMove(300, 250);
        h.Pump();
        Assert.DoesNotContain(h.Find<RenderParagraph>(), p => p.PlainText == "Hello tip");
    }

    [Fact]
    public void TooltipDoesNotAppearIfThePointerLeavesEarly()
    {
        var h = new Harness(Top(new Tooltip("Hello tip", new SizedBox(100, 40, new ColoredBox(Colors.Red)))));
        h.Gestures.PointerMove(50, 20);
        h.Advance(200);
        h.Gestures.PointerMove(300, 250);
        h.Advance(800);
        Assert.DoesNotContain(h.Find<RenderParagraph>(), p => p.PlainText == "Hello tip");
    }

    // ---- SelectableText ----

    [Fact]
    public void SelectableTextCanBeSelectedAndCopied()
    {
        var h = new Harness(Top(new SelectableText("select this text")));
        h.Gestures.PointerDown(300, 10);
        h.Gestures.PointerUp(300, 10);
        h.Binding.Focus.Traverse(1);
        h.Binding.Ctrl = true;
        h.Binding.KeyDown("a", "KeyA");
        h.Binding.KeyDown("c", "KeyC");
        h.Binding.Ctrl = false;
        Assert.Equal("select this text", h.Binding.GetClipboard());
    }
}

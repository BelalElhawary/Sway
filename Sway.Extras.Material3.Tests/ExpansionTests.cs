using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class ExpansionTests
{
    [Fact]
    public void TileHidesItsChildrenUntilTapped()
    {
        bool? changed = null;
        var h = new Harness(new MaterialApp(Top(new ExpansionTile(new Text("Details"), [new Text("Body")], onExpansionChanged: v => changed = v))));
        h.Pump();
        Assert.DoesNotContain("Body", Texts(h));

        TapText(h, "Details");
        h.Advance(300);
        h.Pump();
        Assert.Contains("Body", Texts(h));
        Assert.True(changed);

        TapText(h, "Details");
        h.Advance(300);
        h.Pump();
        Assert.DoesNotContain("Body", Texts(h));
        Assert.False(changed);
    }

    [Fact]
    public void TileCanStartExpanded()
    {
        var h = new Harness(new MaterialApp(Top(new ExpansionTile(new Text("Details"), [new Text("Body")], initiallyExpanded: true))));
        h.Pump();
        Assert.Contains("Body", Texts(h));
    }

    [Fact]
    public void PanelListReportsTheTappedPanelAndShowsOnlyOpenBodies()
    {
        var open = new[] { false, true };
        (int, bool)? reported = null;
        var host = new TestHost(self => Top(new ExpansionPanelList(
        [
            new ExpansionPanel(new Text("One"), new Text("Body one"), open[0]),
            new ExpansionPanel(new Text("Two"), new Text("Body two"), open[1]),
        ], (i, v) => { reported = (i, v); open[i] = v; self.Refresh!(); })));
        var h = new Harness(new MaterialApp(host));
        h.Pump();
        Assert.DoesNotContain("Body one", Texts(h));
        Assert.Contains("Body two", Texts(h));

        TapText(h, "One");
        h.Advance(300);
        h.Pump();
        Assert.Equal((0, true), reported);
        Assert.Contains("Body one", Texts(h));
    }
}

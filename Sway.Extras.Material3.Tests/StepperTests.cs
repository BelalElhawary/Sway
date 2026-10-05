using Xunit;

using Sway.Widgets;
using Sway.Widgets.Tests;
using static Sway.Extras.Material3.Tests.TestHost;

namespace Sway.Extras.Material3.Tests;

public class StepperTests
{
    static IReadOnlyList<Step> Steps() =>
    [
        new Step(new Text("Account"), new Text("Account form")),
        new Step(new Text("Address"), new Text("Address form")),
        new Step(new Text("Confirm"), new Text("Confirm form")),
    ];

    static List<string> After(Widget stepper, int height = 600)
    {
        var h = new Harness(new MaterialApp(Top(stepper)), 400, height);
        h.Pump();
        h.Advance(300);
        h.Pump();
        return Texts(h);
    }

    [Theory]
    [InlineData(StepperType.Vertical)]
    [InlineData(StepperType.Horizontal)]
    public void OnlyTheCurrentStepShowsItsContent(StepperType type)
    {
        var texts = After(new Stepper(Steps(), 1, type: type));
        Assert.Contains("Address form", texts);
        Assert.DoesNotContain("Account form", texts);
        Assert.DoesNotContain("Confirm form", texts);
        Assert.Contains("Continue", texts);
    }

    [Fact]
    public void LastStepOffersFinishInsteadOfContinue()
    {
        var texts = After(new Stepper(Steps(), 2));
        Assert.Contains("Finish", texts);
        Assert.DoesNotContain("Continue", texts);
    }

    [Fact]
    public void TappingAStepTitleAndTheButtonsReportBack()
    {
        int? tapped = null;
        bool continued = false, cancelled = false;
        var h = new Harness(new MaterialApp(Top(new Stepper(Steps(), 0, i => tapped = i, () => continued = true, () => cancelled = true))), 400, 600);
        h.Pump();
        h.Advance(300);
        h.Pump();
        TapText(h, "Confirm");
        Assert.Equal(2, tapped);
        TapText(h, "Continue");
        Assert.True(continued);
        TapText(h, "Cancel");
        Assert.True(cancelled);
    }

    [Fact]
    public void DisabledStepsIgnoreTaps()
    {
        int? tapped = null;
        var steps = new List<Step>(Steps()) { [1] = new Step(new Text("Address"), new Text("Address form"), State: StepState.Disabled) };
        var h = new Harness(new MaterialApp(Top(new Stepper(steps, 0, i => tapped = i))), 400, 600);
        h.Pump();
        TapText(h, "Address");
        Assert.Null(tapped);
    }
}

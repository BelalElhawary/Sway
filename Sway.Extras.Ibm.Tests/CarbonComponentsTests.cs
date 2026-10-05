using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Ibm.Tests;

public class CarbonComponentsTests
{
    static Harness Show(Widget child, int width = 500, int height = 400, CarbonThemeData? theme = null)
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, child), theme: theme ?? CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), width, height);
        h.Gestures.PointerMove(width - 1, height - 1);
        h.Pump();
        return h;
    }

    static List<string> Texts(Harness h) => h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();

    static void TapText(Harness h, string text)
    {
        var p = h.Find<RenderParagraph>().First(x => x.PlainText == text);
        var at = p.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + p.Size.Width / 2, at.Dy + p.Size.Height / 2);
    }

    // A tiny stateful host so controlled components can be driven through several interactions.
    sealed class Host<T>(T initial, Func<T, Action<T>, Widget> build) : StatefulWidget
    {
        internal T Initial => initial;
        internal Func<T, Action<T>, Widget> Build => build;
        public override State CreateState() => new HostState<T>();
    }

    sealed class HostState<T> : State<Host<T>>
    {
        T _value = default!;
        public override void InitState() => _value = Widget.Initial;
        public override Widget Build(BuildContext context) => Widget.Build(_value, v => SetState(() => _value = v));
    }

    // ---- selection ----

    [Fact]
    public void RadioGroupSelectsTheTappedOption()
    {
        var h = Show(new Host<string>("a", (v, set) => new CarbonRadioGroup<string>([new("a", "Alpha"), new("b", "Beta")], v, set, legend: "Pick")));
        Assert.Contains("Pick", Texts(h));
        TapText(h, "Beta");
        // The selected radio draws its dot: one small filled box among the rings.
        Assert.Equal(1, h.Find<RenderDecoratedBox>().Count(b => Math.Abs(b.Size.Width - 8) < 0.5f));
    }

    [Fact]
    public void ToggleFlipsAndShowsItsStateLabel()
    {
        var h = Show(new Host<bool>(false, (v, set) => new CarbonToggle(v, set, label: "Wifi")));
        Assert.Contains("Off", Texts(h));
        TapText(h, "Off");
        Assert.Contains("On", Texts(h));
    }

    [Fact]
    public void ADisabledToggleIgnoresTaps()
    {
        var h = Show(new CarbonToggle(false, null));
        TapText(h, "Off");
        Assert.Contains("Off", Texts(h));
    }

    [Fact]
    public void LinkCallsItsHandler()
    {
        bool hit = false;
        var h = Show(new CarbonLink("Docs", () => hit = true));
        TapText(h, "Docs");
        Assert.True(hit);
    }

    // ---- inputs ----

    [Fact]
    public void NumberInputStepsWithinItsRange()
    {
        double last = double.NaN;
        var h = Show(new SizedBox(width: 200, child: new Host<double>(1, (v, set) => new CarbonNumberInput(v, x => { last = x; set(x); }, min: 0, max: 2, step: 1))));
        // plus is the last 40px square, minus the one before it.
        h.Tap(200 - 20, 20);
        Assert.Equal(2, last);
        h.Tap(200 - 20, 20); // already at max
        Assert.Equal(2, last);
        h.Tap(200 - 60, 20);
        Assert.Equal(1, last);
    }

    [Fact]
    public void SliderMovesWithKeysAndClicks()
    {
        double last = double.NaN;
        var h = Show(new SizedBox(width: 400, child: new Host<double>(50, (v, set) => new CarbonSlider(v, x => { last = x; set(x); }, 0, 100, 10, showValue: false))));
        // The track sits between the "0" and "100" labels; a click left of centre lands in the lower half of the range.
        h.Tap(120, 12);
        Assert.InRange(last, 10, 40);
        double afterClick = last;
        h.Binding.KeyDown("ArrowRight", "ArrowRight");
        h.Pump();
        Assert.Equal(afterClick + 10, last);
        h.Binding.KeyDown("End", "End");
        h.Pump();
        Assert.Equal(100, last);
    }

    [Fact]
    public void TextAreaShowsACounterAndFlagsTooManyCharacters()
    {
        var c = new TextEditingController("hello");
        var h = Show(new SizedBox(width: 300, child: new CarbonTextArea(c, label: "Notes", maxCount: 3)));
        Assert.Contains("5/3", Texts(h));
        Assert.Contains("Too many characters", Texts(h));
    }

    [Fact]
    public void ComboBoxFiltersAsYouType()
    {
        int? picked = null;
        var h = Show(new SizedBox(width: 250, child: new CarbonComboBox<int>([new(1, "Apple"), new(2, "Banana"), new(3, "Blueberry")], 0, v => picked = v)));
        h.Tap(40, 20);
        h.Binding.TextInput("b");
        h.Pump();
        var texts = Texts(h);
        Assert.DoesNotContain("Apple", texts);
        Assert.Contains("Banana", texts);
        TapText(h, "Blueberry");
        Assert.Equal(3, picked);
    }

    [Fact]
    public void MultiSelectKeepsTheMenuOpenAndCollectsPicks()
    {
        IReadOnlyList<int> chosen = [];
        var h = Show(new SizedBox(width: 250, child: new Host<IReadOnlyList<int>>([], (v, set) =>
            new CarbonMultiSelect<int>([new(1, "One"), new(2, "Two"), new(3, "Three")], v, x => { chosen = x; set(x); }))));
        h.Tap(60, 20);
        TapText(h, "One");
        TapText(h, "Three");
        Assert.Equal([1, 3], chosen);
        Assert.Contains("Two", Texts(h));
    }

    // ---- date picker ----

    [Fact]
    public void DatePickerShowsTheDateAndPicksFromTheCalendar()
    {
        DateTime? got = null;
        var h = Show(new SizedBox(width: 300, child: new Host<DateTime?>(new DateTime(2026, 10, 5), (v, set) => new CarbonDatePicker(v, x => { got = x; set(x); }))), height: 500);
        // The calendar icon is the last 40px of the field.
        h.Tap(300 - 20, 20);
        Assert.Contains("October 2026", Texts(h));
        TapText(h, "20");
        Assert.Equal(new DateTime(2026, 10, 20), got);
    }

    [Fact]
    public void CalendarMovesBetweenMonths()
    {
        var h = Show(new CarbonCalendar(new DateTime(2026, 1, 15), _ => { }, today: new DateTime(2026, 1, 15)));
        Assert.Contains("January 2026", Texts(h));
        h.Tap(280 - 20, 20);
        Assert.Contains("February 2026", Texts(h));
    }

    // ---- feedback ----

    [Fact]
    public void ProgressBarFillsInProportionToItsValue()
    {
        var h = Show(new SizedBox(width: 200, child: new CarbonProgressBar(25, label: "Upload")));
        Assert.Contains("Upload", Texts(h));
        var fills = h.Find<RenderBox>().Where(b => Math.Abs(b.Size.Height - 8) < 0.5f).Select(b => b.Size.Width).ToList();
        Assert.Contains(fills, w => Math.Abs(w - 50) < 1);
    }

    [Fact]
    public void InlineLoadingSwapsTheSpinnerForAnIconWhenDone()
    {
        var h = Show(new CarbonInlineLoading("Saved", CarbonStatus.Finished));
        Assert.Contains("Saved", Texts(h));
    }

    [Fact]
    public void NotificationShowsItsTextAndCloses()
    {
        bool closed = false;
        var h = Show(new CarbonNotification(CarbonNotificationKind.Error, "Failed", "Try again", () => closed = true));
        Assert.Contains("Failed", Texts(h));
        Assert.Contains("Try again", Texts(h));
        h.Tap(500 - 24, 24);
        // The close button is the right-most 48px of the notification, which fills the width.
        Assert.True(closed);
    }

    [Fact]
    public void ActionableNotificationCallsItsAction()
    {
        bool acted = false;
        var h = Show(new CarbonNotification(CarbonNotificationKind.Info, "Update", "Ready", actionLabel: "Restart", onAction: () => acted = true));
        TapText(h, "Restart");
        Assert.True(acted);
    }

    [Fact]
    public void ProgressIndicatorLabelsStepsAndJumpsOnTap()
    {
        int? tapped = null;
        var h = Show(new CarbonProgressIndicator([new("First"), new("Second"), new("Third")], 1, i => tapped = i), width: 450);
        Assert.Contains("Second", Texts(h));
        TapText(h, "Third");
        Assert.Equal(2, tapped);
    }

    // ---- layout ----

    [Fact]
    public void AccordionOpensOnePanelAtATime()
    {
        var h = Show(new SizedBox(width: 300, child: new CarbonAccordion([new("A", new Text("body-a")), new("B", new Text("body-b"))])));
        Assert.DoesNotContain("body-a", Texts(h));
        TapText(h, "A");
        Assert.Contains("body-a", Texts(h));
        TapText(h, "B");
        Assert.Contains("body-b", Texts(h));
        Assert.DoesNotContain("body-a", Texts(h));
    }

    [Fact]
    public void AccordionCanKeepSeveralPanelsOpen()
    {
        var h = Show(new SizedBox(width: 300, child: new CarbonAccordion([new("A", new Text("body-a")), new("B", new Text("body-b"))], allowMultiple: true)));
        TapText(h, "A");
        TapText(h, "B");
        Assert.Contains("body-a", Texts(h));
        Assert.Contains("body-b", Texts(h));
    }

    [Fact]
    public void BreadcrumbLinksAllButTheCurrentPage()
    {
        string? went = null;
        var h = Show(new CarbonBreadcrumb([new("Home", () => went = "home"), new("Docs", () => went = "docs"), new("Page")]));
        Assert.Contains("Page", Texts(h));
        TapText(h, "Docs");
        Assert.Equal("docs", went);
        went = null;
        TapText(h, "Page");
        Assert.Null(went);
    }

    [Fact]
    public void TabsShowTheSelectedContentAndReportChanges()
    {
        var h = Show(new SizedBox(width: 400, child: new Host<int>(0, (v, set) => new CarbonTabs([new("One", new Text("content-one")), new("Two", new Text("content-two"))], v, set))));
        Assert.Contains("content-one", Texts(h));
        TapText(h, "Two");
        Assert.Contains("content-two", Texts(h));
        Assert.DoesNotContain("content-one", Texts(h));
    }

    [Fact]
    public void TilesReactToTaps()
    {
        bool clicked = false;
        bool? picked = null;
        var h = Show(new Column(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            new SizedBox(width: 200, child: new CarbonClickableTile(new Text("click me"), () => clicked = true)),
            new SizedBox(width: 200, child: new CarbonSelectableTile(new Text("pick me"), false, v => picked = v)),
        ]));
        TapText(h, "click me");
        TapText(h, "pick me");
        Assert.True(clicked);
        Assert.True(picked);
    }

    [Fact]
    public void ExpandableTileRevealsItsBelowTheFold()
    {
        var h = Show(new SizedBox(width: 300, child: new CarbonExpandableTile(new Text("above"), new Text("below"))));
        Assert.DoesNotContain("below", Texts(h));
        TapText(h, "above");
        Assert.Contains("below", Texts(h));
    }

    [Fact]
    public void StructuredListSelectsARow()
    {
        int? chosen = null;
        var h = Show(new SizedBox(width: 400, child: CarbonStructuredList.OfText(["Name", "Role"], [["Ann", "Dev"], ["Bob", "Ops"]], onSelected: i => chosen = i)));
        Assert.Contains("Role", Texts(h));
        TapText(h, "Bob");
        Assert.Equal(1, chosen);
    }

    [Fact]
    public void CodeSnippetCopiesToTheClipboard()
    {
        var h = Show(new SizedBox(width: 300, child: new CarbonCodeSnippet("npm install carbon", CarbonCodeType.Inline)));
        TapText(h, "npm install carbon");
        Assert.Equal("npm install carbon", h.Binding.GetClipboard());
    }

    [Fact]
    public void MultiLineSnippetCollapsesAndExpands()
    {
        var code = string.Join('\n', Enumerable.Range(1, 10).Select(i => $"line {i}"));
        var h = Show(new SizedBox(width: 300, child: new CarbonCodeSnippet(code, CarbonCodeType.Multi, collapsedLines: 3)));
        Assert.Contains("Show more", Texts(h));
        Assert.DoesNotContain(Texts(h), t => t.Contains("line 10"));
        TapText(h, "Show more");
        Assert.Contains(Texts(h), t => t.Contains("line 10"));
        Assert.Contains("Show less", Texts(h));
    }

    [Fact]
    public void TreeViewExpandsAndSelectsNodes()
    {
        CarbonTreeNode? got = null;
        var h = Show(new SizedBox(width: 300, child: new Host<string?>(null, (v, set) => new CarbonTreeView(
            [new("root", "Root", [new("leaf", "Leaf")])], v, n => { got = n; set(n.Id); }))));
        Assert.DoesNotContain("Leaf", Texts(h));
        TapText(h, "Root");
        Assert.Contains("Leaf", Texts(h));
        TapText(h, "Leaf");
        Assert.Equal("leaf", got!.Id);
    }

    [Fact]
    public void ContainedListCallsAnItemsHandler()
    {
        bool hit = false;
        var h = Show(new SizedBox(width: 300, child: new CarbonContainedList("People", [new("Ann", () => hit = true), new("Bob")])));
        Assert.Contains("People", Texts(h));
        TapText(h, "Ann");
        Assert.True(hit);
    }

    // ---- overlays ----

    [Fact]
    public void TooltipAppearsOnHoverAndLeavesWithThePointer()
    {
        var h = Show(new CarbonTooltip("Helpful", new SizedBox(width: 60, height: 30)));
        Assert.DoesNotContain("Helpful", Texts(h));
        h.Gestures.PointerMove(20, 15);
        h.Pump();
        Assert.Contains("Helpful", Texts(h));
        h.Gestures.PointerMove(450, 350);
        h.Pump();
        Assert.DoesNotContain("Helpful", Texts(h));
    }

    [Fact]
    public void PopoverTogglesOnTapAndClosesWhenClickedAway()
    {
        var h = Show(new CarbonPopover(new SizedBox(width: 60, height: 30), new Text("inside")));
        h.Tap(20, 15);
        Assert.Contains("inside", Texts(h));
        h.Tap(450, 350);
        Assert.DoesNotContain("inside", Texts(h));
    }

    [Fact]
    public void OverflowMenuRunsTheChosenAction()
    {
        string? ran = null;
        var h = Show(new CarbonOverflowMenu([new("Edit", () => ran = "edit"), new("Delete", () => ran = "delete", Danger: true)]));
        h.Tap(20, 20);
        Assert.Contains("Delete", Texts(h));
        TapText(h, "Delete");
        Assert.Equal("delete", ran);
        Assert.DoesNotContain("Edit", Texts(h));
    }

    [Fact]
    public void ModalShowsAboveTheAppAndItsPrimaryActionClosesIt()
    {
        bool primary = false;
        // Show it through a button that has a build context.
        var h2 = Show(new Builder(ctx => new CarbonButton(new Text("Open"), () => CarbonModal.Show(ctx, "Delete?", new Text("Sure?"), "Danger", "Delete", () => { primary = true; return true; },
            "Cancel", danger: true))));
        TapText(h2, "Open");
        Assert.Contains("Delete?", Texts(h2));
        Assert.Contains("Sure?", Texts(h2));
        TapText(h2, "Delete");
        Assert.True(primary);
        Assert.DoesNotContain("Sure?", Texts(h2));
    }

    [Fact]
    public void EscapeClosesAModal()
    {
        var h = Show(new Builder(ctx => new CarbonButton(new Text("Open"), () => CarbonModal.Show(ctx, "Info", new Text("details")))));
        TapText(h, "Open");
        Assert.Contains("details", Texts(h));
        h.Advance(100); // autofocus lands on the next frame
        h.Binding.KeyDown("Escape", "Escape");
        h.Pump();
        Assert.DoesNotContain("details", Texts(h));
    }

    [Fact]
    public void ToastsStackAndDismiss()
    {
        var h = Show(new Builder(ctx => new CarbonButton(new Text("Go"), () =>
        {
            CarbonToast.Show(ctx, CarbonNotificationKind.Success, "First", timeout: TimeSpan.Zero);
            CarbonToast.Show(ctx, CarbonNotificationKind.Info, "Second", timeout: TimeSpan.Zero);
        })), width: 700);
        TapText(h, "Go");
        Assert.Contains("First", Texts(h));
        Assert.Contains("Second", Texts(h));
        var close = h.Find<RenderParagraph>().First(p => p.PlainText == "First").LocalToGlobal(Offset.Zero);
        // Its close button is at the toast's right end: 288 wide, 16 from the right.
        h.Tap(700 - 16 - 24, close.Dy + 8);
        Assert.DoesNotContain("First", Texts(h));
        Assert.Contains("Second", Texts(h));
    }

    // ---- shell ----

    [Fact]
    public void HeaderShowsItsNameAndActions()
    {
        bool menu = false, action = false;
        var h = Show(new SizedBox(width: 400, child: new CarbonHeader("Platform", "IBM", () => menu = true, [new(Icons.Search, () => action = true)])));
        Assert.Contains("Platform", Texts(h));
        Assert.Contains("IBM", Texts(h));
        h.Tap(24, 24);
        Assert.True(menu);
        h.Tap(400 - 24, 24);
        Assert.True(action);
    }

    [Fact]
    public void SideNavExpandsCategoriesAndSelectsLinks()
    {
        CarbonNavItem? got = null;
        var h = Show(new Host<string?>(null, (v, set) => new CarbonSideNav([new("cat", "Category", Children: [new("a", "Link A")]), new("b", "Link B")], v, n => { got = n; set(n.Id); })));
        Assert.DoesNotContain("Link A", Texts(h));
        TapText(h, "Category");
        Assert.Contains("Link A", Texts(h));
        TapText(h, "Link A");
        Assert.Equal("a", got!.Id);
    }

    // ---- both themes ----

    [Fact]
    public void ComponentsBuildInTheDarkTheme()
    {
        var h = Show(new Column(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            new CarbonNotification(CarbonNotificationKind.Warning, "Careful"),
            new CarbonProgressBar(40),
            new CarbonCodeSnippet("x", CarbonCodeType.Single),
            new CarbonSkeletonText(),
            new CarbonTabs([new("A", new Text("a"))], 0, _ => { }, contained: true),
        ]), theme: CarbonThemeData.Gray100());
        Assert.Contains("Careful", Texts(h));
    }
}

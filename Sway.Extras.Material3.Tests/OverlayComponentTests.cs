using Xunit;

using Sway.Extras.Material3;
using Sway.Widgets;
using Sway.Widgets.Tests;
namespace Sway.Extras.Material3.Tests;

public class OverlayComponentTests
{
    static (Harness harness, Func<BuildContext> context) App(Widget body, int height = 300)
    {
        BuildContext? captured = null;
        var root = new MaterialApp(new Builder(ctx =>
        {
            captured = ctx;
            return body;
        }));
        return (new Harness(root, 400, height), () => captured!);
    }

    static List<string> Texts(Harness h) => h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();

    static void Open(Harness h)
    {
        h.Pump();
        h.Advance(350);
        h.Pump();
    }

    static Offset CenterOfText(Harness h, string text)
    {
        var p = h.Find<RenderParagraph>().First(r => r.PlainText == text);
        var o = p.LocalToGlobal(Offset.Zero);
        return new Offset(o.Dx + p.Size.Width / 2, o.Dy + p.Size.Height / 2);
    }

    static IReadOnlyList<MenuEntry<string>> Items() =>
    [
        new MenuItem<string>("copy", new Text("Copy")),
        new MenuItem<string>("paste", new Text("Paste"), Enabled: false),
        new MenuDivider<string>(),
        new MenuItem<string>("delete", new Text("Delete")),
    ];

    // ---- menus ----

    [Fact]
    public void MenuShowsItsItemsAndSelectsOnClick()
    {
        string? chosen = null;
        var (h, ctx) = App(new SizedBox());
        Menus.Show(ctx(), new Rect(20, 20, 100, 40), Items(), v => chosen = v);
        Open(h);
        Assert.Equal(new[] { "Copy", "Paste", "Delete" }, Texts(h));
        var at = CenterOfText(h, "Delete");
        h.Tap(at.Dx, at.Dy);
        Assert.Equal("delete", chosen);
        h.Pump();
        Assert.DoesNotContain("Copy", Texts(h));
    }

    [Fact]
    public void DisabledMenuItemsCannotBeSelected()
    {
        string? chosen = null;
        var (h, ctx) = App(new SizedBox());
        Menus.Show(ctx(), new Rect(20, 20, 100, 40), Items(), v => chosen = v);
        Open(h);
        var at = CenterOfText(h, "Paste");
        h.Tap(at.Dx, at.Dy);
        Assert.Null(chosen);
    }

    [Fact]
    public void MenuKeyboardNavigationSkipsDisabledItems()
    {
        string? chosen = null;
        var (h, ctx) = App(new SizedBox());
        Menus.Show(ctx(), new Rect(20, 20, 100, 40), Items(), v => chosen = v);
        Open(h);
        h.Binding.KeyDown("ArrowDown", "ArrowDown"); // Copy
        h.Binding.KeyDown("ArrowDown", "ArrowDown"); // skips disabled Paste -> Delete
        h.Binding.KeyDown("Enter", "Enter");
        Assert.Equal("delete", chosen);
    }

    [Fact]
    public void ClickingOutsideOrEscapeClosesTheMenu()
    {
        string? chosen = null;
        var (h, ctx) = App(new SizedBox());
        Menus.Show(ctx(), new Rect(20, 20, 100, 40), Items(), v => chosen = v);
        Open(h);
        h.Tap(380, 280);
        h.Pump();
        Assert.DoesNotContain("Copy", Texts(h));

        Menus.Show(ctx(), new Rect(20, 20, 100, 40), Items(), v => chosen = v);
        Open(h);
        h.Binding.KeyDown("Escape", "Escape");
        h.Pump();
        Assert.DoesNotContain("Copy", Texts(h));
        Assert.Null(chosen);
    }

    [Fact]
    public void MenuOpensAboveWhenThereIsNoRoomBelow()
    {
        var (h, ctx) = App(new SizedBox());
        Menus.Show(ctx(), new Rect(20, 270, 100, 20), Items(), _ => { });
        Open(h);
        var copy = CenterOfText(h, "Copy");
        Assert.True(copy.Dy < 270, $"menu should sit above the anchor, copy is at {copy.Dy}");
    }

    [Fact]
    public void PopupMenuButtonOpensFromItsOwnPosition()
    {
        string? chosen = null;
        var (h, _) = App(new Align(Alignment.TopLeft, new PopupMenuButton<string>(Items(), v => chosen = v)));
        h.Tap(20, 20);
        Open(h);
        Assert.Contains("Copy", Texts(h));
        var at = CenterOfText(h, "Copy");
        h.Tap(at.Dx, at.Dy);
        Assert.Equal("copy", chosen);
    }

    // ---- drawer and bottom sheet ----

    [Fact]
    public void DrawerSlidesInFromTheStartAndClosesOnTheScrim()
    {
        var (h, ctx) = App(new SizedBox());
        Panels.ShowDrawer(ctx(), (_, close) => new NavigationDrawer(0, [new NavigationDestination(Icons.Home, "Home"), new NavigationDestination(Icons.Settings, "Settings")]),
            width: 200);
        Open(h);
        Assert.Contains("Settings", Texts(h));
        var settings = CenterOfText(h, "Settings");
        Assert.True(settings.Dx < 200, "the drawer sits at the left edge");

        h.Tap(350, 150); // scrim
        h.Advance(500);
        Assert.DoesNotContain("Settings", Texts(h));
    }

    [Fact]
    public void DrawerSelectionCallsBack()
    {
        int selected = -1;
        var (h, ctx) = App(new SizedBox());
        Panels.ShowDrawer(ctx(), (_, close) => new NavigationDrawer(0,
            [new NavigationDestination(Icons.Home, "Home"), new NavigationDestination(Icons.Settings, "Settings")], i => { selected = i; close(); }));
        Open(h);
        var at = CenterOfText(h, "Settings");
        h.Tap(at.Dx, at.Dy);
        h.Advance(500);
        Assert.Equal(1, selected);
        Assert.DoesNotContain("Settings", Texts(h));
    }

    [Fact]
    public void EndDrawerSitsOnTheOppositeEdge()
    {
        var (h, ctx) = App(new SizedBox());
        Panels.ShowDrawer(ctx(), (_, close) => new NavigationDrawer(0, [new NavigationDestination(Icons.Home, "Home")]), DrawerSide.End, width: 200);
        Open(h);
        Assert.True(CenterOfText(h, "Home").Dx > 200);
    }

    [Fact]
    public void BottomSheetSlidesUpAndEscapeClosesIt()
    {
        var (h, ctx) = App(new SizedBox());
        Panels.ShowBottomSheet(ctx(), (_, close) => new BottomSheet(new Padding(EdgeInsets.All(24), new Text("Sheet content"))));
        Open(h);
        Assert.True(CenterOfText(h, "Sheet content").Dy > 200, "the sheet is at the bottom");
        h.Binding.KeyDown("Escape", "Escape");
        h.Advance(500);
        Assert.DoesNotContain("Sheet content", Texts(h));
    }

    [Fact]
    public void DraggingTheBottomSheetDownDismissesIt()
    {
        var (h, ctx) = App(new SizedBox());
        Panels.ShowBottomSheet(ctx(), (_, close) => new BottomSheet(new SizedBox(height: 120, child: new Text("Sheet content"))));
        Open(h);
        h.Gestures.PointerDown(200, 270);
        h.Gestures.PointerMove(200, 290);
        h.Gestures.PointerMove(200, 330);
        h.Gestures.PointerMove(200, 420);
        h.Gestures.PointerUp(200, 420);
        h.Advance(500);
        Assert.DoesNotContain("Sheet content", Texts(h));
    }

    [Fact]
    public void ANonDismissibleSheetIgnoresTheScrim()
    {
        var (h, ctx) = App(new SizedBox());
        Panels.ShowBottomSheet(ctx(), (_, close) => new BottomSheet(new Text("Sheet content")), dismissible: false);
        Open(h);
        h.Tap(200, 20);
        h.Binding.KeyDown("Escape", "Escape");
        h.Advance(500);
        Assert.Contains("Sheet content", Texts(h));
    }

    // ---- date picker ----

    [Fact]
    public void DatePickerShowsTheInitialDateAndMonth()
    {
        var (h, ctx) = App(new SizedBox(), height: 700);
        DatePicker.Show(ctx(), new DateTime(2026, 3, 15), new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), _ => { });
        Open(h);
        var texts = Texts(h);
        Assert.Contains("Sun, Mar 15", texts);
        Assert.Contains("March 2026", texts);
        Assert.Contains("31", texts);
        Assert.DoesNotContain("32", texts);
    }

    [Fact]
    public void DatePickerSelectsADayAndConfirms()
    {
        DateTime? chosen = null;
        var (h, ctx) = App(new SizedBox(), height: 700);
        DatePicker.Show(ctx(), new DateTime(2026, 3, 15), new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), d => chosen = d);
        Open(h);
        var day = CenterOfText(h, "22");
        h.Tap(day.Dx, day.Dy);
        Assert.Contains("Sun, Mar 22", Texts(h));
        var ok = CenterOfText(h, "OK");
        h.Tap(ok.Dx, ok.Dy);
        Assert.Equal(new DateTime(2026, 3, 22), chosen);
        h.Advance(300);
        Assert.DoesNotContain("March 2026", Texts(h));
    }

    [Fact]
    public void DatePickerCancelReturnsNothing()
    {
        DateTime? chosen = null;
        var (h, ctx) = App(new SizedBox(), height: 700);
        DatePicker.Show(ctx(), new DateTime(2026, 3, 15), new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), d => chosen = d);
        Open(h);
        var cancel = CenterOfText(h, "Cancel");
        h.Tap(cancel.Dx, cancel.Dy);
        h.Advance(300);
        Assert.Null(chosen);
        Assert.DoesNotContain("March 2026", Texts(h));
    }

    [Fact]
    public void DatePickerNavigatesMonthsWithinTheAllowedRange()
    {
        var (h, ctx) = App(new SizedBox(), height: 700);
        DatePicker.Show(ctx(), new DateTime(2026, 3, 15), new DateTime(2026, 3, 10), new DateTime(2026, 4, 20), _ => { });
        Open(h);
        // Day 5 is before the first allowed date, so it cannot be chosen.
        var five = CenterOfText(h, "5");
        h.Tap(five.Dx, five.Dy);
        Assert.Contains("Sun, Mar 15", Texts(h));

        // The forward arrow of the month header sits at the right of the 328px dialog.
        h.Tap(332, 262);
        Assert.Contains("April 2026", Texts(h));
    }

    [Fact]
    public void DatePickerClampsAnInitialDateOutsideTheRange()
    {
        var (h, ctx) = App(new SizedBox(), height: 700);
        DatePicker.Show(ctx(), new DateTime(2019, 1, 1), new DateTime(2026, 3, 10), new DateTime(2026, 4, 20), _ => { });
        Open(h);
        Assert.Contains("Tue, Mar 10", Texts(h));
    }
}

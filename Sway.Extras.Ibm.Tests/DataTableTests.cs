using Sway.Extras.Ibm;
using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Ibm.Tests;

public class DataTableTests
{
    sealed record Item(string Name, int Amount);

    // Names are unsorted on purpose so sorting visibly reorders them.
    static readonly Item[] Items = Enumerable.Range(1, 25).Select(i => new Item($"row-{(i * 7) % 25 + 1:00}", i * 10)).ToArray();

    static readonly DataColumn<Item>[] Columns =
    [
        new("Name", r => r.Name),
        DataColumn<Item>.By("Amount", r => r.Amount, align: TextAlign.End),
    ];

    static Harness Show(IReadOnlyList<Item>? rows = null, bool selectable = false, bool searchable = false, int? pageSize = null,
        Action<IReadOnlyList<Item>>? onSelection = null, IReadOnlyList<BatchAction<Item>>? batch = null) =>
        new(new CarbonApp(new SingleChildScrollView(new DataTable<Item>(Columns, rows ?? Items, title: "Things", size: TableSize.Medium,
                selectable: selectable, searchable: searchable, pageSize: pageSize, onSelectionChanged: onSelection, batchActions: batch)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 900, 900);

    static List<RenderParagraph> Paragraphs(Harness h) => h.Find<RenderParagraph>();
    static List<string> Texts(Harness h) => Paragraphs(h).Select(p => p.PlainText).ToList();
    static List<string> Names(Harness h) => Texts(h).Where(t => t.StartsWith("row-")).ToList();

    static void TapText(Harness h, string text)
    {
        var p = Paragraphs(h).First(x => x.PlainText == text);
        var at = p.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + p.Size.Width / 2, at.Dy + p.Size.Height / 2);
    }

    [Fact]
    public void ShowsTheTitleHeadersAndEveryRowWithoutPaging()
    {
        var h = Show();
        var texts = Texts(h);
        Assert.Contains("Things", texts);
        Assert.Contains("Name", texts);
        Assert.Contains("Amount", texts);
        Assert.Equal(25, Names(h).Count);
    }

    [Fact]
    public void PagingShowsOnePageAndMovesWithTheButtons()
    {
        var h = Show(pageSize: 10);
        Assert.Equal(10, Names(h).Count);
        Assert.Contains("1–10 of 25 items", Texts(h));
        Assert.Contains("1 of 3 pages", Texts(h));

        // The next button is the last thing in the bar.
        var bar = Paragraphs(h).First(p => p.PlainText == "1 of 3 pages");
        float y = bar.LocalToGlobal(Offset.Zero).Dy + bar.Size.Height / 2;
        h.Tap(h.Width - 20, y);
        Assert.Contains("11–20 of 25 items", Texts(h));
        h.Tap(h.Width - 20, y);
        Assert.Contains("21–25 of 25 items", Texts(h));
        Assert.Equal(5, Names(h).Count);
    }

    [Fact]
    public void ClickingAHeaderSortsAscendingThenDescendingThenClears()
    {
        var h = Show();
        var original = Names(h);
        TapText(h, "Name");
        Assert.Equal(original.OrderBy(n => n).ToList(), Names(h));
        TapText(h, "Name");
        Assert.Equal(original.OrderByDescending(n => n).ToList(), Names(h));
        TapText(h, "Name");
        Assert.Equal(original, Names(h));
    }

    [Fact]
    public void NumericColumnsSortByValueNotText()
    {
        var h = Show(rows: [new Item("a", 9), new Item("b", 100), new Item("c", 20)]);
        TapText(h, "Amount");
        var amounts = Texts(h).Where(t => t is "9" or "100" or "20").ToList();
        Assert.Equal(["9", "20", "100"], amounts);
    }

    [Fact]
    public void SearchFiltersRowsAndReportsNoResults()
    {
        var h = Show(searchable: true);
        var field = h.Find<RenderEditable>().First();
        var at = field.LocalToGlobal(Offset.Zero);
        h.Tap(at.Dx + 5, at.Dy + 5);
        h.Binding.TextInput("row-07");
        h.Pump();
        Assert.Equal(["row-07"], Names(h));

        h.Binding.TextInput("zzz");
        h.Pump();
        Assert.Contains("No matching results", Texts(h));
        Assert.Empty(Names(h));
    }

    [Fact]
    public void SelectingRowsRaisesTheCallbackAndShowsTheBatchBar()
    {
        IReadOnlyList<Item>? picked = null;
        Item[]? deleted = null;
        var h = Show(selectable: true, onSelection: r => picked = r,
            batch: [new BatchAction<Item>("Delete", rows => deleted = rows.ToArray())]);

        var first = Paragraphs(h).First(p => p.PlainText == Names(h)[0]);
        float y = first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2;
        h.Tap(24, y);
        Assert.Single(picked!);
        Assert.Contains("1 item selected", Texts(h));

        TapText(h, "Delete");
        Assert.Single(deleted!);

        TapText(h, "Cancel");
        Assert.Empty(picked!);
        Assert.DoesNotContain("1 item selected", Texts(h));
    }

    [Fact]
    public void TheHeaderCheckboxSelectsEverythingOnThePage()
    {
        IReadOnlyList<Item>? picked = null;
        var h = Show(selectable: true, pageSize: 10, onSelection: r => picked = r);
        var header = Paragraphs(h).First(p => p.PlainText == "Name");
        h.Tap(24, header.LocalToGlobal(Offset.Zero).Dy + header.Size.Height / 2);
        Assert.Equal(10, picked!.Count);
        Assert.Contains("10 items selected", Texts(h));
    }

    [Fact]
    public void RowHeightFollowsTheTableSize()
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(Columns, Items.Take(3).ToList(), size: TableSize.ExtraSmall)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 600, 400);
        var ys = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Select(p => p.LocalToGlobal(Offset.Zero).Dy).ToList();
        Assert.Equal(24, ys[1] - ys[0], 1);
    }

    [Fact]
    public void ColumnsThatDoNotFitScrollSidewaysInsteadOfSqueezing()
    {
        DataColumn<Item>[] wide = [new("A", r => r.Name, Width: 250), new("B", r => r.Name, Width: 250), new("C", r => r.Name, Width: 250)];
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(wide, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 400, 300);
        float XOf(string t) => Paragraphs(h).First(p => p.PlainText == t).LocalToGlobal(Offset.Zero).Dx;
        Assert.True(XOf("C") > 400, "the third column starts off screen");
        h.Gestures.PointerScroll(200, 100, 300, 0);
        h.Advance(600); // wheel scrolling is animated
        Assert.True(XOf("C") < 400, "scrolling sideways brings it into view");
        Assert.True(XOf("A") < 0, "and moves the first column out");
    }

    [Fact]
    public void AVerticalWheelOverAWideTableDoesNotScrollItSideways()
    {
        DataColumn<Item>[] wide = [new("A", r => r.Name, Width: 250), new("B", r => r.Name, Width: 250), new("C", r => r.Name, Width: 250)];
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(wide, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 400, 300);
        h.Gestures.PointerScroll(200, 100, 0, 200);
        h.Advance(600);
        Assert.Equal(16, Paragraphs(h).First(p => p.PlainText == "A").LocalToGlobal(Offset.Zero).Dx, 1);
    }

    [Fact]
    public void ColumnsThatFitDoNotScroll()
    {
        var h = Show();
        h.Gestures.PointerScroll(200, 100, 300, 0);
        h.Advance(600);
        // Still at the left edge: just the cell padding.
        Assert.Equal(16, Paragraphs(h).First(p => p.PlainText == "Name").LocalToGlobal(Offset.Zero).Dx, 1);
    }

    [Fact]
    public void ExpandingARowShowsItsDetail()
    {
        var h = new Harness(new CarbonApp(new SingleChildScrollView(new DataTable<Item>(Columns, Items.Take(5).ToList(), size: TableSize.Medium,
            rowDetail: r => new Text("detail of " + r.Name), detailHeight: 60)), theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 900, 700);
        var first = Paragraphs(h).First(p => p.PlainText.StartsWith("row-"));
        float y = first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2;
        float before = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Skip(1).First().LocalToGlobal(Offset.Zero).Dy;
        Assert.DoesNotContain(Texts(h), t => t.StartsWith("detail of"));

        h.Tap(24, y);
        Assert.Single(Texts(h), t => t.StartsWith("detail of"));
        // The row below moved down by the detail panel and its divider.
        float after = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Skip(1).First().LocalToGlobal(Offset.Zero).Dy;
        Assert.Equal(61, after - before, 1);

        h.Tap(24, y);
        Assert.DoesNotContain(Texts(h), t => t.StartsWith("detail of"));
    }

    [Fact]
    public void HeaderCheckboxShowsAPartialSelection()
    {
        var h = Show(selectable: true);
        var header = Paragraphs(h).First(p => p.PlainText == "Name");
        float hy = header.LocalToGlobal(Offset.Zero).Dy + header.Size.Height / 2;
        var fill = CarbonColors.White.IconPrimary;
        // Inside the 16px box, clear of its border and of the dash.
        using (var before = h.Render()) Assert.NotEqual(fill, before.GetPixel(19, (int)hy));

        var first = Paragraphs(h).First(p => p.PlainText.StartsWith("row-"));
        h.Tap(24, first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2);
        // Selecting a row opens the batch bar above the table, so the header has moved down.
        var movedHeader = Paragraphs(h).First(p => p.PlainText == "Name");
        float after_y = movedHeader.LocalToGlobal(Offset.Zero).Dy + movedHeader.Size.Height / 2;
        using var after = h.Render();
        Assert.Equal(fill, after.GetPixel(19, (int)after_y));
    }

    static Harness ShowWide(int width = 400, bool resizable = false, int sticky = 0, DataColumn<Item>[]? columns = null, bool selectable = false) =>
        new(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(columns ?? WideColumns, Items.Take(3).ToList(), size: TableSize.Medium,
            resizable: resizable, stickyColumns: sticky, selectable: selectable)), theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), width, 300);

    static readonly DataColumn<Item>[] WideColumns =
        [new("A", r => r.Name, Width: 250), new("B", r => r.Name, Width: 250), new("C", r => r.Name, Width: 250)];

    static float XOf(Harness h, string t) => Paragraphs(h).First(p => p.PlainText == t).LocalToGlobal(Offset.Zero).Dx;
    static float YOf(Harness h, string t) { var p = Paragraphs(h).First(x => x.PlainText == t); return p.LocalToGlobal(Offset.Zero).Dy + p.Size.Height / 2; }

    [Fact]
    public void DraggingAHeaderEdgeResizesItsColumn()
    {
        var h = ShowWide(width: 900, resizable: true);
        float bBefore = XOf(h, "B");
        // A's right edge is at 250.
        h.Gestures.PointerDown(247, YOf(h, "A"));
        h.Gestures.PointerMove(277, YOf(h, "A"));
        h.Gestures.PointerMove(307, YOf(h, "A"));
        h.Gestures.PointerUp(307, YOf(h, "A"));
        h.Pump();
        Assert.Equal(bBefore + 60, XOf(h, "B"), 1);
    }

    [Fact]
    public void ADraggedColumnNeverGetsNarrowerThanItsMinimum()
    {
        var h = ShowWide(width: 900, resizable: true);
        float y = YOf(h, "A");
        h.Gestures.PointerDown(247, y);
        h.Gestures.PointerMove(100, y);
        h.Gestures.PointerMove(-300, y);
        h.Gestures.PointerUp(-300, y);
        h.Pump();
        Assert.Equal(48, XOf(h, "B") - XOf(h, "A"), 1);
    }

    [Fact]
    public void WithoutResizableAHeaderEdgeDoesNotDrag()
    {
        var h = ShowWide(width: 900);
        float bBefore = XOf(h, "B");
        h.Gestures.PointerDown(247, YOf(h, "A"));
        h.Gestures.PointerMove(307, YOf(h, "A"));
        h.Gestures.PointerUp(307, YOf(h, "A"));
        h.Pump();
        Assert.Equal(bBefore, XOf(h, "B"), 1);
    }

    [Fact]
    public void StickyColumnsStayInViewWhileTheRestScroll()
    {
        var h = ShowWide(sticky: 1);
        Assert.Equal(16, XOf(h, "A"), 1);
        h.Gestures.PointerScroll(300, 100, 300, 0);
        h.Advance(600);
        Assert.Equal(16, XOf(h, "A"), 1);
        Assert.True(XOf(h, "C") < 400, "the last column scrolled into view");
        Assert.True(XOf(h, "B") < 250, "the unpinned columns moved");
    }

    [Fact]
    public void StickyColumnsKeepTheSelectionColumnPinnedToo()
    {
        var h = ShowWide(width: 600, sticky: 1, selectable: true);
        float a = XOf(h, "A");
        h.Gestures.PointerScroll(300, 100, 300, 0);
        h.Advance(600);
        Assert.Equal(a, XOf(h, "A"), 1);
    }

    [Fact]
    public void AShiftedWheelAndATrackpadSidewaysSwipeBothScrollSideways()
    {
        // A mouse wheel with shift held reports its movement on the horizontal axis; a trackpad swipe reports both axes with the larger one dominant.
        // The table only reads the deltas, so both reach it as a horizontal delta.
        var h = ShowWide();
        h.Gestures.PointerScroll(200, 100, 200, 0);
        h.Advance(600);
        float afterSideways = XOf(h, "A");
        Assert.True(afterSideways < 16);

        var h2 = ShowWide();
        h2.Gestures.PointerScroll(200, 100, 200, 5);
        h2.Advance(600);
        Assert.True(XOf(h2, "A") < 16, "a mostly-horizontal swipe with a little vertical drift still scrolls sideways");
    }

    [Fact]
    public void DoubleClickingAnEditableCellEditsItAndEnterCommits()
    {
        (Item, string)? edited = null;
        DataColumn<Item>[] cols = [new("Name", r => r.Name, OnEdit: (r, t) => edited = (r, t)), DataColumn<Item>.By("Amount", r => r.Amount)];
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(cols, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 600, 300);
        var name = Names(h)[0];
        float y = YOf(h, name);
        h.Tap(40, y);
        h.Tap(40, y);
        h.Advance(100);
        Assert.Single(h.Find<RenderEditable>());

        h.Binding.TextInput("!");
        h.Pump();
        h.Binding.Focus.HandleKey(new KeyEvent("Enter", "Enter", true, false, false, false, false));
        h.Pump();
        Assert.NotNull(edited);
        Assert.EndsWith("!", edited!.Value.Item2);
        Assert.Empty(h.Find<RenderEditable>());
    }

    [Fact]
    public void EscapeCancelsAnEdit()
    {
        bool edited = false;
        DataColumn<Item>[] cols = [new("Name", r => r.Name, OnEdit: (_, _) => edited = true)];
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(cols, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 600, 300);
        float y = YOf(h, Names(h)[0]);
        h.Tap(40, y);
        h.Tap(40, y);
        h.Advance(100);
        h.Binding.TextInput("x");
        h.Binding.Focus.HandleKey(new KeyEvent("Escape", "Escape", true, false, false, false, false));
        h.Pump();
        Assert.False(edited);
        Assert.Empty(h.Find<RenderEditable>());
    }

    [Fact]
    public void ColumnsWithoutOnEditCannotBeEdited()
    {
        var h = Show();
        float y = YOf(h, Names(h)[0]);
        h.Tap(40, y);
        h.Tap(40, y);
        h.Advance(100);
        Assert.Empty(h.Find<RenderEditable>());
    }

    [Fact]
    public void ADetailPanelWithoutAHeightSizesToItsContent()
    {
        var h = new Harness(new CarbonApp(new SingleChildScrollView(new DataTable<Item>(Columns, Items.Take(5).ToList(), size: TableSize.Medium,
            rowDetail: r => new SizedBox(height: 120, child: new Text("detail of " + r.Name)))), theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 900, 900);
        float before = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Skip(1).First().LocalToGlobal(Offset.Zero).Dy;
        var first = Paragraphs(h).First(p => p.PlainText.StartsWith("row-"));
        h.Tap(24, first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2);
        float after = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Skip(1).First().LocalToGlobal(Offset.Zero).Dy;
        // 120 of content, 16 of padding top and bottom, and the divider.
        Assert.Equal(120 + 32 + 1, after - before, 1);
    }

    [Fact]
    public void ACappedBodyWithContentSizedDetailsStillShrinksToItsRows()
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new DataTable<Item>(Columns, Items.Take(2).ToList(), size: TableSize.Medium,
            maxBodyHeight: 400, rowDetail: r => new Text("detail of " + r.Name))), theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 600, 600);
        var first = Paragraphs(h).First(p => p.PlainText.StartsWith("row-"));
        h.Tap(24, first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2);
        var table = h.Find<RenderViewport>().Last();
        Assert.True(table.Size.Height < 400, "two rows and one short panel need far less than the cap");
    }

    [Fact]
    public void CompactTagsAreShorter()
    {
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new CarbonTag("Running", CarbonTagColor.Green, compact: true)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 300, 100);
        Assert.Equal(18, h.Find<RenderDecoratedBox>().Last().Size.Height, 1);
    }

    [Fact]
    public void TagsUseTheirLabelAndCanBeDismissed()
    {
        bool closed = false;
        var h = new Harness(new CarbonApp(new Align(Alignment.TopLeft, new CarbonTag("Running", CarbonTagColor.Green, () => closed = true)),
            theme: CarbonThemeData.White(), themeMode: CarbonThemeMode.Light), 300, 100);
        Assert.Contains("Running", Texts(h));
        var box = h.Find<RenderDecoratedBox>().Last();
        Assert.Equal(24, box.Size.Height, 1);
        h.Tap(box.Size.Width - 12, 12);
        Assert.True(closed);
    }
}

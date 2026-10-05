using Sway.Extras.Ibm;
using Xunit;

namespace Sway.Widgets.Tests;

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
        new(new MaterialApp(new SingleChildScrollView(new DataTable<Item>(Columns, rows ?? Items, title: "Things", size: TableSize.Medium,
                selectable: selectable, searchable: searchable, pageSize: pageSize, onSelectionChanged: onSelection, batchActions: batch)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 900, 900);

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
        var h = new Harness(new MaterialApp(new Align(Alignment.TopLeft, new DataTable<Item>(Columns, Items.Take(3).ToList(), size: TableSize.ExtraSmall)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 600, 400);
        var ys = Paragraphs(h).Where(p => p.PlainText.StartsWith("row-")).Select(p => p.LocalToGlobal(Offset.Zero).Dy).ToList();
        Assert.Equal(24, ys[1] - ys[0], 1);
    }

    [Fact]
    public void ColumnsThatDoNotFitScrollSidewaysInsteadOfSqueezing()
    {
        DataColumn<Item>[] wide = [new("A", r => r.Name, Width: 250), new("B", r => r.Name, Width: 250), new("C", r => r.Name, Width: 250)];
        var h = new Harness(new MaterialApp(new Align(Alignment.TopLeft, new DataTable<Item>(wide, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 400, 300);
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
        var h = new Harness(new MaterialApp(new Align(Alignment.TopLeft, new DataTable<Item>(wide, Items.Take(3).ToList(), size: TableSize.Medium)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 400, 300);
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
        var h = new Harness(new MaterialApp(new SingleChildScrollView(new DataTable<Item>(Columns, Items.Take(5).ToList(), size: TableSize.Medium,
            rowDetail: r => new Text("detail of " + r.Name), detailHeight: 60)), theme: IbmTheme.White(), themeMode: ThemeMode.Light), 900, 700);
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
        var primary = IbmTheme.White().ColorScheme.Primary;
        using (var before = h.Render()) Assert.NotEqual(primary, before.GetPixel(16, (int)hy));

        var first = Paragraphs(h).First(p => p.PlainText.StartsWith("row-"));
        h.Tap(24, first.LocalToGlobal(Offset.Zero).Dy + first.Size.Height / 2);
        using var after = h.Render();
        Assert.Equal(primary, after.GetPixel(16, (int)hy));
    }

    [Fact]
    public void CompactTagsAreShorter()
    {
        var h = new Harness(new MaterialApp(new Align(Alignment.TopLeft, new Tag("Running", TagColor.Green, compact: true)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 300, 100);
        Assert.Equal(18, h.Find<RenderDecoratedBox>().Last().Size.Height, 1);
    }

    [Fact]
    public void TagsUseTheirLabelAndCanBeDismissed()
    {
        bool closed = false;
        var h = new Harness(new MaterialApp(new Align(Alignment.TopLeft, new Tag("Running", TagColor.Green, () => closed = true)),
            theme: IbmTheme.White(), themeMode: ThemeMode.Light), 300, 100);
        Assert.Contains("Running", Texts(h));
        var box = h.Find<RenderDecoratedBox>().Last();
        Assert.Equal(24, box.Size.Height, 1);
        h.Tap(box.Size.Width - 12, 12);
        Assert.True(closed);
    }
}

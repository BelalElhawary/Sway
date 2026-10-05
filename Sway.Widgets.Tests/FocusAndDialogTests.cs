using Xunit;

namespace Sway.Widgets.Tests;

public class FocusAndDialogTests
{
    static (Harness harness, Func<BuildContext> context) App(Widget body)
    {
        BuildContext? captured = null;
        var root = new MaterialApp(new Builder(ctx =>
        {
            captured = ctx;
            return body;
        }));
        return (new Harness(root), () => captured!);
    }

    static FocusNode Field(string label) => new() { DebugLabel = label };

    static Widget FocusBox(FocusNode node, string label) =>
        new SizedBox(100, 30, new Focus(new Text(label), node));

    static Widget Form(params FocusNode[] nodes) =>
        new Column(nodes.Select(n => FocusBox(n, n.DebugLabel!)).ToList());

    // ---- traversal ----

    [Fact]
    public void TabVisitsFocusablesInTreeOrder()
    {
        FocusNode a = Field("a"), b = Field("b"), c = Field("c");
        var (h, _) = App(Form(a, b, c));
        h.Binding.KeyDown("Tab", "Tab");
        Assert.Same(a, h.Binding.Focus.Primary);
        h.Binding.KeyDown("Tab", "Tab");
        Assert.Same(b, h.Binding.Focus.Primary);
        h.Binding.Shift = true;
        h.Binding.KeyDown("Tab", "Tab");
        h.Binding.Shift = false;
        Assert.Same(a, h.Binding.Focus.Primary);
    }

    [Fact]
    public void FocusTraversalOrderOverridesTreeOrder()
    {
        FocusNode a = Field("a"), b = Field("b"), c = Field("c");
        var (h, _) = App(new Column(
        [
            FocusBox(a, "a"),
            new FocusTraversalOrder(-1, FocusBox(b, "b")),
            new FocusTraversalOrder(1, FocusBox(c, "c")),
        ]));
        var visited = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            h.Binding.KeyDown("Tab", "Tab");
            visited.Add(h.Binding.Focus.Primary!.DebugLabel!);
        }
        Assert.Equal(new[] { "b", "a", "c" }, visited);
    }

    [Fact]
    public void KeyboardFocusScrollsTheFieldIntoView()
    {
        var controller = new ScrollController();
        var nodes = Enumerable.Range(0, 20).Select(i => Field("n" + i)).ToList();
        var h = new Harness(new SingleChildScrollView(new Column(nodes.Select(n => FocusBox(n, n.DebugLabel!)).ToList()), controller: controller));
        Assert.Equal(0, controller.Offset);
        // 20 rows of 30px in a 300px window: tabbing to the 15th must scroll.
        for (int i = 0; i < 15; i++)
        {
            h.Binding.KeyDown("Tab", "Tab");
            h.Pump();
        }
        Assert.True(controller.Offset > 0, "the scrollable followed the focus");
        var box = (RenderBox)nodes[14].Element!.FindRenderObject()!;
        float top = box.LocalToGlobal(Offset.Zero).Dy;
        Assert.InRange(top, 0, 300 - 30 + 0.5f);
    }

    [Fact]
    public void ClickingDoesNotScrollTheFocusedFieldIntoView()
    {
        var controller = new ScrollController();
        var nodes = Enumerable.Range(0, 20).Select(i => Field("n" + i)).ToList();
        var h = new Harness(new SingleChildScrollView(new Column(nodes.Select(n => FocusBox(n, n.DebugLabel!)).ToList()), controller: controller));
        nodes[19].RequestFocus();
        h.Pump();
        Assert.Equal(0, controller.Offset);
    }

    static Widget Hit() => new GestureDetector(onTap: () => { }, behavior: HitTestBehavior.Opaque, child: new SizedBox());

    [Fact]
    public void PressingOutsideATextFieldDropsItsFocus()
    {
        var node = new FocusNode { DebugLabel = "field", OnTextInput = _ => { } };
        var h = new Harness(new Column([new SizedBox(100, 30, new Focus(Hit(), node)), new SizedBox(100, 100, Hit())]));
        node.RequestFocus();
        h.Pump();
        Assert.Same(node, h.Binding.Focus.Primary);
        h.Tap(200, 15);
        Assert.Same(node, h.Binding.Focus.Primary);
        h.Tap(200, 100);
        Assert.Null(h.Binding.Focus.Primary);
    }

    [Fact]
    public void LocalToGlobalIncludesTheScrollOffset()
    {
        var controller = new ScrollController();
        var node = Field("n");
        var h = new Harness(new SingleChildScrollView(new Column([new SizedBox(100, 500), FocusBox(node, "n")]), controller: controller));
        float before = ((RenderBox)node.Element!.FindRenderObject()!).LocalToGlobal(Offset.Zero).Dy;
        controller.JumpTo(100);
        h.Pump();
        Assert.Equal(before - 100, ((RenderBox)node.Element!.FindRenderObject()!).LocalToGlobal(Offset.Zero).Dy, 0.5f);
    }

    // ---- dialogs ----

    [Fact]
    public void EscapeClosesADismissibleDialog()
    {
        var (h, ctx) = App(new SizedBox());
        Dialogs.Show(ctx(), (_, close) => new AlertDialog(new Text("Title"), new Text("Body")));
        h.Pump();
        h.Advance(300);
        Assert.Contains(h.Find<RenderParagraph>(), p => p.PlainText == "Title");
        h.Binding.KeyDown("Escape", "Escape");
        h.Pump();
        Assert.DoesNotContain(h.Find<RenderParagraph>(), p => p.PlainText == "Title");
    }

    [Fact]
    public void EscapeDoesNotCloseAModalDialog()
    {
        var (h, ctx) = App(new SizedBox());
        Dialogs.Show(ctx(), (_, close) => new AlertDialog(new Text("Title")), barrierDismissible: false);
        h.Advance(300);
        h.Binding.KeyDown("Escape", "Escape");
        h.Pump();
        Assert.Contains(h.Find<RenderParagraph>(), p => p.PlainText == "Title");
    }

    [Fact]
    public void DialogTrapsTabFocusInsideItself()
    {
        FocusNode behind = Field("behind"), inA = Field("in-a"), inB = Field("in-b");
        var (h, ctx) = App(Form(behind));
        Dialogs.Show(ctx(), (_, close) => new AlertDialog(content: new Column(
            [FocusBox(inA, "in-a"), FocusBox(inB, "in-b")], mainAxisSize: MainAxisSize.Min)));
        h.Pump();
        h.Advance(300);

        var visited = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            h.Binding.KeyDown("Tab", "Tab");
            visited.Add(h.Binding.Focus.Primary!.DebugLabel!);
        }
        Assert.DoesNotContain("behind", visited);
        Assert.Contains("in-a", visited);
        Assert.Contains("in-b", visited);
    }

    [Fact]
    public void ClosingTheDialogRestoresFocus()
    {
        FocusNode behind = Field("behind");
        var (h, ctx) = App(Form(behind));
        behind.RequestFocus();
        h.Pump();
        var close = Dialogs.Show(ctx(), (_, c) => new AlertDialog(new Text("T")));
        h.Pump();
        h.Advance(300);
        Assert.NotSame(behind, h.Binding.Focus.Primary);
        close();
        h.Pump();
        Assert.Same(behind, h.Binding.Focus.Primary);
    }

    // ---- snack bars ----

    [Fact]
    public void SnackBarsAreShownOneAtATime()
    {
        var (h, ctx) = App(new SizedBox());
        Dialogs.ShowSnackBar(ctx(), "first", duration: TimeSpan.FromSeconds(1));
        Dialogs.ShowSnackBar(ctx(), "second", duration: TimeSpan.FromSeconds(1));
        h.Advance(300);
        var texts = h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();
        Assert.Contains("first", texts);
        Assert.DoesNotContain("second", texts);

        h.Advance(1200);
        texts = h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();
        Assert.DoesNotContain("first", texts);
        Assert.Contains("second", texts);

        h.Advance(1500);
        Assert.DoesNotContain(h.Find<RenderParagraph>(), p => p.PlainText == "second");
    }

    [Fact]
    public void ACancelledQueuedSnackBarIsSkipped()
    {
        var (h, ctx) = App(new SizedBox());
        Dialogs.ShowSnackBar(ctx(), "one", duration: TimeSpan.FromSeconds(1));
        var cancel = Dialogs.ShowSnackBar(ctx(), "two", duration: TimeSpan.FromSeconds(1));
        Dialogs.ShowSnackBar(ctx(), "three", duration: TimeSpan.FromSeconds(1));
        cancel();
        h.Advance(1500);
        var texts = h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();
        Assert.DoesNotContain("two", texts);
        Assert.Contains("three", texts);
        h.Advance(3000);
    }
}

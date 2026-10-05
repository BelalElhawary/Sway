using System.Reflection;
using Sway.Extras.Material3;
using Sway.Widgets;
using Sway.Widgets.Tests;
using Xunit;

namespace Sway.Extras.Material3.Tests;

public class RtlEditingTests
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    static (Harness harness, RenderEditable render, TextEditState state) Field(TextDirection direction, string text = "", float width = 300)
    {
        var h = new Harness(new Align(Alignment.TopLeft, new Directionality(direction,
            new SizedBox(width: width, child: new TextField(new TextEditingController(text), autofocus: true)))), (int)width + 20, 80);
        h.Advance(500);
        var render = h.Find<RenderEditable>()[0];
        var state = (TextEditState)typeof(RenderEditable).GetField("_state", Flags)!.GetValue(render)!;
        return (h, render, state);
    }

    // Caret x in field coordinates, read the same way the painter places it.
    static float CaretX(RenderEditable render, TextEditState state)
    {
        var offset = typeof(RenderEditable).GetMethod("CaretPosition", Flags)!.Invoke(render, new object[] { state.Caret })!;
        return (float)offset.GetType().GetProperty("Dx")!.GetValue(offset)!;
    }

    [Fact]
    public void EmptyRtlFieldPutsTheCaretOnTheRight()
    {
        var (_, render, state) = Field(TextDirection.Rtl);
        Assert.Equal(render.Size.Width, CaretX(render, state), 1);
    }

    [Fact]
    public void EmptyLtrFieldPutsTheCaretOnTheLeft()
    {
        var (_, render, state) = Field(TextDirection.Ltr);
        Assert.Equal(0, CaretX(render, state), 1);
    }

    static float LineWidth(RenderEditable render)
    {
        var lines = (System.Collections.IList)typeof(RenderEditable).GetField("_lines", Flags)!.GetValue(render)!;
        return (float)lines[0]!.GetType().GetProperty("Width")!.GetValue(lines[0])!;
    }

    [Fact]
    public void RtlCaretStaysAtTheTypedEndAsTextGrows()
    {
        var (h, render, state) = Field(TextDirection.Rtl);
        foreach (var ch in "ابتا")
        {
            h.Binding.TextInput(ch.ToString());
            h.Pump();
            // The text is right-aligned, so the logical end of the typed text sits at the text's left edge.
            Assert.Equal(render.Size.Width - LineWidth(render), CaretX(render, state), 1);
        }
    }

    [Fact]
    public void RtlSelectionRunsFromTheRightEdgeToTheLeftEdge()
    {
        var (h, render, state) = Field(TextDirection.Rtl, "ابت");
        state.MoveTo(0, false);
        h.Pump();
        Assert.Equal(render.Size.Width, CaretX(render, state), 1);

        state.MoveTo(3, false);
        h.Pump();
        Assert.Equal(render.Size.Width - LineWidth(render), CaretX(render, state), 1);
    }

    [Fact]
    public void ArrowKeysMoveOnScreenInRtlText()
    {
        var (h, render, state) = Field(TextDirection.Rtl, "ابت");
        Assert.Equal(3, state.Caret);
        float atEnd = CaretX(render, state);

        // Right moves toward the right edge, which is the previous logical character in RTL.
        h.Binding.KeyDown("ArrowRight", "ArrowRight");
        h.Pump();
        Assert.Equal(2, state.Caret);
        Assert.True(CaretX(render, state) > atEnd);

        h.Binding.KeyDown("ArrowLeft", "ArrowLeft");
        h.Pump();
        Assert.Equal(3, state.Caret);
    }

    // Horizontal extent of the selection highlight (tinted pixels) in the rendered frame.
    static (int min, int max) HighlightSpan(Harness h)
    {
        using var bmp = h.Render();
        int min = int.MaxValue, max = -1;
        for (int x = 6; x < 294; x++)
            for (int y = 22; y < 34; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Blue - c.Green > 15) { min = Math.Min(min, x); max = Math.Max(max, x); break; }
            }
        return (min, max);
    }

    [Fact]
    public void SelectionInMixedRtlTextSitsOnTheCorrectSideOfEachRun()
    {
        string arabic = "السلام عليكم ";
        string english = "god bless you";
        var (h, render, state) = Field(TextDirection.Rtl, arabic + english);

        state.MoveTo(arabic.Length, false);
        state.MoveTo(arabic.Length + english.Length, true);
        h.Pump();
        var (eMin, eMax) = HighlightSpan(h);
        Assert.True(eMax > eMin + 50, $"English highlight collapsed: {eMin}-{eMax}");

        state.MoveTo(0, false);
        state.MoveTo(arabic.Length, true);
        h.Pump();
        var (aMin, aMax) = HighlightSpan(h);
        Assert.True(aMax > aMin + 50, $"Arabic highlight collapsed: {aMin}-{aMax}");
        Assert.True(eMax <= aMin + 2, $"English {eMin}-{eMax} must sit left of Arabic {aMin}-{aMax}");
    }

    [Fact]
    public void TypingASpaceAfterEnglishDoesNotMoveTheCaretAcrossTheLine()
    {
        var (h, render, state) = Field(TextDirection.Rtl);
        foreach (var ch in "بلال Belal") { h.Binding.TextInput(ch.ToString()); h.Pump(); }
        float before = CaretX(render, state);
        h.Binding.TextInput(" "); h.Pump();
        // The space extends the English to the left of the Arabic; the caret must stay next to the Arabic.
        Assert.InRange(CaretX(render, state), before - 1, before + 1);
    }

    [Fact]
    public void TypedEnglishNeverOverlapsTheArabicBesideIt()
    {
        var (h, render, state) = Field(TextDirection.Rtl, width: 440);
        foreach (var ch in "بلال محمد وجدي Belal Mohamed Wagdy")
        {
            h.Binding.TextInput(ch.ToString());
            h.Pump();
        }
        // Before the fix the English ran into the Arabic; a blank column must separate them near the caret.
        float caret = CaretX(render, state);
        using var bmp = h.Render();
        bool blank = false;
        for (int x = (int)caret - 3; x <= (int)caret + 7 && !blank; x++)
        {
            bool ink = false;
            for (int y = 22; y < 34 && !ink; y++) { var c = bmp.GetPixel(x, y); ink = c.Red < 160 && c.Green < 160 && !(c.Blue - c.Green > 15); }
            blank = !ink;
        }
        Assert.True(blank, "English and Arabic touch");
    }

    [Theory]
    [InlineData(TextDirection.Rtl)]
    [InlineData(TextDirection.Ltr)]
    public void EmptyFocusedFieldPaintsItsCaret(TextDirection direction)
    {
        var (h, _, _) = Field(direction);
        using var bmp = h.Render();
        bool found = false;
        // The caret is the primary-colored vertical bar inside the box; the border and hint are excluded by the row/column range.
        for (int x = 6; x < 294 && !found; x++)
            for (int y = 24; y < 32 && !found; y++) { var c = bmp.GetPixel(x, y); found = c.Blue - c.Green > 40 && c.Red < 140; }
        Assert.True(found, "no caret pixels inside an empty focused field");
    }
}

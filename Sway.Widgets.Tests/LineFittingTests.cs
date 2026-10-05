using System.Reflection;
using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimization 2.2: wrapping a long paragraph must break at exactly the same places as the old per-prefix scan.</summary>
public class LineFittingTests
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    static string Rep(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    public static IEnumerable<object[]> Samples() =>
    [
        ["sentence", Rep("The quick brown fox jumps over the lazy dog. ", 12)],
        ["longword", Rep("abcdefghij", 40)],
        ["marks", Rep("héllo wórld \U0001F600 ", 30)],
        ["paragraphs", "first paragraph that is long enough to wrap around the edge\n\nsecond one also wraps around the edge of the box\nshort"],
        ["spaces", Rep("a  b   c    ", 40)],
        ["short", "fits"],
        ["empty", ""],
    ];

    internal static (Harness harness, RenderEditable render, TextEditState state) Area(string text, float width = 200)
    {
        var controller = new TextEditingController(text);
        var h = new Harness(new Align(Alignment.TopLeft, new Directionality(TextDirection.Ltr,
            new SizedBox(width: width, child: new EditableText(controller, maxLines: null)))), (int)width + 20, 400);
        h.Pump();
        var render = h.Find<RenderEditable>()[0];
        var state = (TextEditState)typeof(RenderEditable).GetField("_state", Flags)!.GetValue(render)!;
        return (h, render, state);
    }

    internal static string Breaks(TextEditState state) =>
        string.Join("|", state.Lines.Select(l => $"{l.Start}-{l.End}{(l.HardBreak ? "!" : "")}"));

    // Captured from the per-prefix FitLine before it was rewritten.
    static readonly Dictionary<string, string> Golden = new()
    {
        ["short"] = "0-4!",
        ["marks"] = "0-38|38-70|70-102|102-134|134-166|166-198|198-230|230-262|262-294|294-326|326-358|358-390|390-422|422-454|454-480!",
        ["paragraphs"] = "0-29|29-55|55-59!|60-60!|61-90|90-109!|110-115!",
        ["longword"] = "0-30|30-60|60-90|90-120|120-150|150-180|180-210|210-240|240-270|270-300|300-330|330-360|360-390|390-400!",
        ["spaces"] = "0-43|43-87|87-131|131-175|175-219|219-263|263-307|307-351|351-395|395-439|439-480!",
        ["empty"] = "0-0!",
        ["sentence"] = "0-26|26-55|55-85|85-110|110-139|139-170|170-200|200-229|229-260|260-290|290-319|319-350|350-380|380-409|409-440|440-470|470-499|499-530|530-540!",
    };

    [Theory, MemberData(nameof(Samples))]
    public void BreaksMatchTheOriginalAlgorithm(string name, string text)
    {
        var (_, _, state) = Area(text);
        Assert.Equal(Golden[name], Breaks(state));
    }

    [Fact]
    public void EveryWrappedLineFitsOrIsASingleGrapheme()
    {
        var (_, render, state) = Area(Rep("The quick brown fox jumps over the lazy dog. ", 40));
        foreach (var line in state.Lines.Where(l => !l.HardBreak))
        {
            string text = state.Value.Substring(line.Start, line.End - line.Start);
            float w = TextShaper.MeasureShaped(text, TextStyle.Fallback.ToFont());
            Assert.True(w <= render.Size.Width + 0.5f || state.NextBoundary(line.Start) >= line.End, $"line {line.Start}-{line.End} is {w}px");
        }
    }

    // ---- optimization 2.3: per-paragraph line cache ----

    static string Doc(int paragraphs) =>
        string.Join((char)10, Enumerable.Range(0, paragraphs).Select(i => $"Paragraph {i}: " + Rep("The quick brown fox jumps over the lazy dog. ", 3)));

    [Fact]
    public void EditingOneParagraphLaysOutOnlyThatParagraph()
    {
        var (h, render, state) = Area(Doc(20));
        h.Tap(20, 10); // focus; the caret lands in the first paragraph
        int before = render.ParagraphsLaidOut;

        h.Binding.TextInput("x");
        h.Pump();

        Assert.Equal(1, render.ParagraphsLaidOut - before);
        Assert.Equal(Doc(20).Length + 1, state.Value.Length);
    }

    [Fact]
    public void MovingTheCaretLaysOutNoParagraphs()
    {
        var (h, render, _) = Area(Doc(20));
        h.Tap(20, 10);
        int before = render.ParagraphsLaidOut;

        h.Tap(120, 10);
        h.Tap(60, 10);

        Assert.Equal(0, render.ParagraphsLaidOut - before);
    }

    [Fact]
    public void CachedLayoutEqualsAFreshLayoutAfterEdits()
    {
        var (h, _, state) = Area(Doc(12));
        h.Tap(20, 10);
        for (int i = 0; i < 40; i++) { h.Binding.TextInput(i % 7 == 0 ? " " : "wide"); h.Pump(); }
        h.Binding.KeyDown("Enter", "Enter"); h.Binding.KeyUp("Enter", "Enter");
        h.Binding.TextInput("new paragraph");
        h.Pump();

        var (_, _, fresh) = Area(state.Value);
        Assert.Equal(Breaks(fresh), Breaks(state));
    }

    [Fact]
    public void IdenticalParagraphsAreLaidOutOnce()
    {
        var paragraph = Rep("The quick brown fox jumps over the lazy dog. ", 3);
        var (_, render, state) = Area(string.Join((char)10, Enumerable.Repeat(paragraph, 10)));
        Assert.True(state.Lines.Count > 10);
        Assert.Equal(1, render.ParagraphsLaidOut);
    }
}

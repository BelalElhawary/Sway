using Xunit;

namespace Sway.Widgets.Tests;

public class TextEditStateTests
{
    [Fact]
    public void StartsWithCaretAtTheEnd()
    {
        var s = new TextEditState("hello", false);
        Assert.Equal(5, s.Caret);
        Assert.False(s.HasSelection);
    }

    [Fact]
    public void InsertReplacesTheSelection()
    {
        var s = new TextEditState("hello world", false);
        s.MoveTo(0, false);
        s.MoveTo(5, true);
        Assert.True(s.Insert("bye"));
        Assert.Equal("bye world", s.Value);
        Assert.Equal(3, s.Caret);
        Assert.False(s.HasSelection);
    }

    [Fact]
    public void SingleLineFlattensNewlines()
    {
        var s = new TextEditState("", false);
        s.Insert("a\r\nb\nc");
        Assert.Equal("a b c", s.Value);
    }

    [Fact]
    public void MultilineNormalisesNewlines()
    {
        var s = new TextEditState("", true);
        s.Insert("a\r\nb\rc");
        Assert.Equal("a\nb\nc", s.Value);
    }

    [Fact]
    public void MaxLengthTruncatesAndRefuses()
    {
        var s = new TextEditState("", false) { MaxLength = 3 };
        Assert.True(s.Insert("abcdef"));
        Assert.Equal("abc", s.Value);
        Assert.False(s.Insert("x"));
    }

    [Fact]
    public void MaxLengthDoesNotSplitASurrogatePair()
    {
        var s = new TextEditState("", false) { MaxLength = 2 };
        s.Insert("a\U0001F600");
        Assert.Equal("a", s.Value);
    }

    [Fact]
    public void BackspaceRemovesAWholeGrapheme()
    {
        var s = new TextEditState("a\U0001F600", false);
        s.DeleteBackward(false);
        Assert.Equal("a", s.Value);

        var combining = new TextEditState("é", false);
        combining.DeleteBackward(false);
        Assert.Equal("", combining.Value);
    }

    [Fact]
    public void DeleteByWord()
    {
        var s = new TextEditState("one two", false);
        s.DeleteBackward(true);
        Assert.Equal("one ", s.Value);

        var f = new TextEditState("one two", false);
        f.MoveTo(0, false);
        f.DeleteForward(true);
        Assert.Equal("two", f.Value);
    }

    [Fact]
    public void DeletesAtTheEdgesDoNothing()
    {
        var s = new TextEditState("ab", false);
        Assert.False(s.DeleteForward(false));
        s.MoveTo(0, false);
        Assert.False(s.DeleteBackward(false));
    }

    [Fact]
    public void ArrowCollapsesSelectionToItsEdge()
    {
        var s = new TextEditState("hello", false);
        s.MoveTo(1, false);
        s.MoveTo(4, true);
        s.MoveHorizontal(-1, false, false);
        Assert.Equal(1, s.Caret);
        Assert.False(s.HasSelection);
    }

    [Fact]
    public void ShiftArrowExtendsTheSelection()
    {
        var s = new TextEditState("hello", false);
        s.MoveTo(0, false);
        s.MoveHorizontal(1, false, true);
        s.MoveHorizontal(1, false, true);
        Assert.Equal("he", s.SelectedText);
    }

    [Fact]
    public void WordNavigation()
    {
        var s = new TextEditState("foo  bar_baz qux", false);
        Assert.Equal(5, s.WordRight(0));
        Assert.Equal(13, s.WordRight(5));
        Assert.Equal(5, s.WordLeft(13));
        Assert.Equal(0, s.WordLeft(3));
    }

    [Fact]
    public void SelectWordAndLine()
    {
        var s = new TextEditState("hello world\nsecond line", true);
        s.SelectWordAt(7);
        Assert.Equal("world", s.SelectedText);
        s.SelectLineAt(15);
        Assert.Equal("second line", s.SelectedText);
        s.SelectAll();
        Assert.Equal(s.Value, s.SelectedText);
    }

    [Fact]
    public void ExternalValueResetsCaretAndBumpsVersion()
    {
        var s = new TextEditState("a", false);
        int version = s.Version;
        Assert.True(s.SetValueExternal("longer"));
        Assert.Equal(6, s.Caret);
        Assert.True(s.Version > version);
        Assert.False(s.SetValueExternal("longer"));
    }

    [Fact]
    public void TypingMergesIntoOneUndoStep()
    {
        var s = new TextEditState("", false);
        foreach (var c in "abc") s.Insert(c.ToString());
        Assert.True(s.Undo());
        Assert.Equal("", s.Value);
        Assert.False(s.Undo());
    }

    [Fact]
    public void PasteIsItsOwnUndoStep()
    {
        var s = new TextEditState("", false);
        s.Insert("a");
        s.Insert("PASTE", "paste");
        s.Undo();
        Assert.Equal("a", s.Value);
    }

    [Fact]
    public void RedoReappliesAnUndoneEdit()
    {
        var s = new TextEditState("", false);
        s.Insert("hi");
        s.Undo();
        Assert.True(s.Redo());
        Assert.Equal("hi", s.Value);
        Assert.False(s.Redo());
    }

    [Fact]
    public void NewEditClearsRedo()
    {
        var s = new TextEditState("", false);
        s.Insert("a");
        s.Undo();
        s.Insert("b");
        Assert.False(s.Redo());
    }

    [Fact]
    public void LineOfIndexPrefersTheFollowingLineAtASoftWrap()
    {
        var s = new TextEditState("abcdef", true);
        s.Lines.Clear();
        s.Lines.Add(new TextLine(0, 3, false));
        s.Lines.Add(new TextLine(3, 6, true));
        Assert.Equal(1, s.LineOfIndex(3));
        Assert.Equal(1, s.LineOfIndex(6));
        Assert.Equal(0, s.LineOfIndex(2));
    }
}

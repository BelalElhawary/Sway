using System.Globalization;
using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimization 2.1: the cached boundary lookups must agree with a fresh scan of the string.</summary>
public class GraphemeBoundaryTests
{
    static int ReferencePrevious(string value, int index)
    {
        if (index <= 0) return 0;
        int previous = 0;
        foreach (int start in StringInfo.ParseCombiningCharacters(value))
        {
            if (start >= index) break;
            previous = start;
        }
        return previous;
    }

    static int ReferenceNext(string value, int index)
    {
        if (index >= value.Length) return value.Length;
        foreach (int start in StringInfo.ParseCombiningCharacters(value))
            if (start > index) return start;
        return value.Length;
    }

    public static IEnumerable<object[]> Samples() =>
    [
        [""],
        ["a"],
        ["hello world"],
        ["line one\nline two\n\nline four"],
        ["cr\r\nlf and\rlone"],
        ["héllo é́"],
        ["emoji \U0001F600 pair \U0001F468‍\U0001F469‍\U0001F467 family"],
        ["مرحبا بالعالم"],
        ["café naïve 你好"],
    ];

    [Theory, MemberData(nameof(Samples))]
    public void BoundariesMatchAFreshScanAtEveryIndex(string value)
    {
        var edit = new TextEditState(value, true);
        for (int i = -1; i <= value.Length + 1; i++)
        {
            Assert.Equal(ReferencePrevious(value, i), edit.PreviousBoundary(i));
            Assert.Equal(ReferenceNext(value, i), edit.NextBoundary(i));
        }
    }

    [Fact]
    public void TheCacheFollowsEdits()
    {
        var edit = new TextEditState("abc", true);
        Assert.Equal(2, edit.PreviousBoundary(3));

        edit.Insert("é");
        Assert.Equal("abcé", edit.Value);
        Assert.Equal(3, edit.PreviousBoundary(edit.Value.Length));
        Assert.Equal(edit.Value.Length, edit.NextBoundary(3));

        Assert.True(edit.Undo());
        Assert.Equal("abc", edit.Value);
        Assert.Equal(2, edit.PreviousBoundary(3));

        edit.SetValueExternal("x\U0001F600y");
        Assert.Equal(1, edit.PreviousBoundary(3));
        Assert.Equal(3, edit.NextBoundary(1));
    }
}

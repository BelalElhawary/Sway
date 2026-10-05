using Xunit;

namespace Sway.Widgets.Tests;

/// <summary>Optimization 2.5: the shape cache evicts by age instead of clearing everything when it fills up.</summary>
public class GenerationCacheTests
{
    static TextShaper.GenerationCache<int, string> Cache(int generationSize, List<string> released) =>
        new(generationSize, released.Add);

    [Fact]
    public void FindsWhatWasAdded()
    {
        var cache = Cache(4, new());
        cache.Add(1, "one");
        Assert.True(cache.TryGetValue(1, out var value));
        Assert.Equal("one", value);
        Assert.False(cache.TryGetValue(2, out _));
    }

    [Fact]
    public void EntriesTouchedEveryGenerationSurviveForever()
    {
        var released = new List<string>();
        var cache = Cache(4, released);
        for (int i = 0; i < 4; i++) cache.Add(i, $"hot{i}");

        // 100 churn entries fill and flip many generations; the hot set is read between every insert.
        for (int n = 100; n < 200; n++)
        {
            for (int i = 0; i < 4; i++) Assert.True(cache.TryGetValue(i, out _), $"hot{i} lost at churn {n}");
            cache.Add(n, $"churn{n}");
        }
        Assert.DoesNotContain(released, r => r.StartsWith("hot"));
    }

    [Fact]
    public void UntouchedEntriesAreReleasedAfterAFullGeneration()
    {
        var released = new List<string>();
        var cache = Cache(2, released);
        cache.Add(1, "a");
        cache.Add(2, "b");
        cache.Add(3, "c"); // flips: a, b move to the previous generation
        Assert.Empty(released);
        cache.Add(4, "d");
        cache.Add(5, "e"); // flips again: a, b were not touched, so they are dropped
        Assert.Equal(new[] { "a", "b" }, released.OrderBy(x => x));
        Assert.False(cache.TryGetValue(1, out _));
        Assert.True(cache.TryGetValue(5, out _));
    }

    [Fact]
    public void SizeStaysBoundedAndNothingIsReleasedTwice()
    {
        var released = new List<string>();
        var cache = Cache(8, released);
        for (int i = 0; i < 1000; i++)
        {
            cache.Add(i, $"v{i}");
            Assert.True(cache.Count <= 16);
        }
        Assert.Equal(released.Count, released.Distinct().Count());
    }
}

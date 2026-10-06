namespace Sway.Widgets;

/// <summary>Builds the page for a matched route.</summary>
public delegate Widget RouteBuilder(BuildContext context, RouteMatch match);

/// <summary>How a page enters and leaves.</summary>
public enum PageTransition
{
    /// <summary>Fades and slides in from the trailing edge (the default for a push).</summary>
    Slide,
    Fade,
    None,
}

/// <summary>
/// One entry of a route table. <paramref name="pattern"/> is a path: literal segments, <c>:name</c> segments that capture a value and a
/// trailing <c>*</c> that captures the rest (<c>/shop/:category/*</c>). Routes are tried in order and the first match wins.
/// </summary>
public sealed class RouteDefinition(string pattern, RouteBuilder builder, Func<RouteMatch, string>? title = null, PageTransition? transition = null)
{
    readonly string[] _segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);

    public string Pattern => pattern;
    internal RouteBuilder Builder => builder;
    internal Func<RouteMatch, string>? Title => title;
    internal PageTransition? Transition => transition;

    internal bool TryMatch(string location, out RouteMatch match)
    {
        match = null!;
        var normalized = RouteMatch.Normalize(location, out var path, out var query);
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, string>();

        for (int i = 0; i < _segments.Length; i++)
        {
            string segment = _segments[i];
            if (segment == "*" && i == _segments.Length - 1)
            {
                values["*"] = string.Join('/', parts.Skip(i).Select(RouteMatch.Decode));
                break;
            }
            if (i >= parts.Length) return false;
            if (segment.StartsWith(':')) values[segment[1..]] = RouteMatch.Decode(parts[i]);
            else if (!string.Equals(segment, parts[i], StringComparison.Ordinal)) return false;
        }
        if (_segments.Length == 0 && parts.Length > 0) return false;
        if (_segments.Length > 0 && _segments[^1] != "*" && parts.Length != _segments.Length) return false;

        match = new RouteMatch(normalized, path, values, query);
        return true;
    }
}

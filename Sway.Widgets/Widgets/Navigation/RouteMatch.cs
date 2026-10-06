namespace Sway.Widgets;

/// <summary>A location resolved against a route: the matched path, its <c>:name</c> segments and its query string.</summary>
public sealed class RouteMatch
{
    static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    internal RouteMatch(string location, string path, IReadOnlyDictionary<string, string> parameters, IReadOnlyDictionary<string, string> query)
    {
        Location = location; Path = path; Params = parameters; Query = query;
    }

    /// <summary>Path and query as written, normalised: <c>/products/42?color=red</c>.</summary>
    public string Location { get; }

    /// <summary>The path alone: <c>/products/42</c>.</summary>
    public string Path { get; }

    /// <summary>Values of the <c>:name</c> segments (and <c>*</c> for a trailing wildcard), percent-decoded.</summary>
    public IReadOnlyDictionary<string, string> Params { get; }

    /// <summary>Query string values, percent-decoded; the last one wins when a key repeats.</summary>
    public IReadOnlyDictionary<string, string> Query { get; }

    public string? Param(string name) => Params.GetValueOrDefault(name);

    internal static RouteMatch Unmatched(string location) => new(Normalize(location, out var path, out var query), path, NoValues, query);

    /// <summary>Makes a location absolute and canonical (<c>a//b/</c> becomes <c>/a/b</c>); splits off the path and query. Fragments are dropped.</summary>
    internal static string Normalize(string location, out string path, out IReadOnlyDictionary<string, string> query)
    {
        int hash = location.IndexOf('#');
        if (hash >= 0) location = location[..hash];
        int q = location.IndexOf('?');
        string rawPath = q >= 0 ? location[..q] : location;
        string rawQuery = q >= 0 ? location[(q + 1)..] : "";

        var segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        path = "/" + string.Join('/', segments);
        var values = new Dictionary<string, string>();
        foreach (var pair in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = Decode(eq >= 0 ? pair[..eq] : pair);
            if (key.Length > 0) values[key] = eq >= 0 ? Decode(pair[(eq + 1)..]) : "";
        }
        query = values;
        return rawQuery.Length > 0 ? path + "?" + rawQuery : path;
    }

    internal static string Decode(string s)
    {
        try { return Uri.UnescapeDataString(s.Replace('+', ' ')); }
        catch (UriFormatException) { return s; }
    }
}

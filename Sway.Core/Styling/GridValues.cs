using System.Globalization;

namespace Sway.Core.Styling;

/// <summary>Parsing for grid track lists and line placement values.</summary>
static class GridValues
{
    public static bool TryParseTemplate(string value, float fontSize, in StyleContext ctx, out TrackTemplate template)
    {
        template = new TrackTemplate();
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var token in CssValues.SplitTokens(value))
        {
            if (token.StartsWith('[')) continue; // named lines are not supported yet

            if (token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
            {
                string inner = token[7..^1];
                int comma = CssParser.TopLevelSplits(inner, ',').FirstOrDefault(-1);
                if (comma < 0) return false;

                string count = inner[..comma].Trim().ToLowerInvariant();
                var pattern = new List<Track>();
                foreach (var t in CssValues.SplitTokens(inner[(comma + 1)..]))
                {
                    if (t.StartsWith('[')) continue;
                    if (!TryParseTrack(t, fontSize, ctx, out var track)) return false;
                    pattern.Add(track);
                }
                if (pattern.Count == 0) return false;

                if (count is "auto-fill" or "auto-fit")
                {
                    if (template.AutoPattern is not null) return false; // only one auto repeat is allowed
                    template.AutoPattern = pattern;
                }
                else if (int.TryParse(count, out int n) && n > 0)
                {
                    var target = template.AutoPattern is null ? template.Before : template.After;
                    for (int i = 0; i < n; i++) target.AddRange(pattern);
                }
                else return false;
                continue;
            }

            if (!TryParseTrack(token, fontSize, ctx, out var single)) return false;
            (template.AutoPattern is null ? template.Before : template.After).Add(single);
        }
        return true;
    }

    public static bool TryParseTrack(string value, float fontSize, in StyleContext ctx, out Track track)
    {
        value = value.Trim();
        track = Track.Auto;

        if (value.StartsWith("minmax(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            string inner = value[7..^1];
            int comma = CssParser.TopLevelSplits(inner, ',').FirstOrDefault(-1);
            if (comma < 0) return false;
            if (!TryParseSize(inner[..comma], fontSize, ctx, out var min) || !TryParseSize(inner[(comma + 1)..], fontSize, ctx, out var max)) return false;
            if (min.Kind == TrackKind.Fr) return false;
            track = new Track(min, max);
            return true;
        }

        if (value.StartsWith("fit-content(", StringComparison.OrdinalIgnoreCase))
        {
            track = new Track(TrackSize.Auto, new TrackSize(TrackKind.MaxContent, 0)); // approximation
            return true;
        }

        if (!TryParseSize(value, fontSize, ctx, out var size)) return false;
        track = size.Kind is TrackKind.Fr or TrackKind.Auto
            ? new Track(TrackSize.Auto, size)
            : new Track(size, size);
        return true;
    }

    static bool TryParseSize(string value, float fontSize, in StyleContext ctx, out TrackSize size)
    {
        value = value.Trim().ToLowerInvariant();
        size = default;

        switch (value)
        {
            case "auto": size = TrackSize.Auto; return true;
            case "min-content": size = new TrackSize(TrackKind.MinContent, 0); return true;
            case "max-content": size = new TrackSize(TrackKind.MaxContent, 0); return true;
        }

        if (value.EndsWith("fr") && float.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fr))
        {
            size = new TrackSize(TrackKind.Fr, fr);
            return true;
        }

        if (!CssValues.TryLength(value, fontSize, ctx, out var len) || len.IsAuto) return false;
        size = len.Unit == LengthUnit.Percent ? new TrackSize(TrackKind.Percent, len.Value) : new TrackSize(TrackKind.Px, len.Value);
        return true;
    }

    public static void ParseLine(string value, out GridLine line)
    {
        value = value.Trim().ToLowerInvariant();
        line = GridLine.Auto;

        if (value.StartsWith("span"))
        {
            string rest = value[4..].Trim();
            line = new GridLine(true, rest.Length == 0 ? 1 : int.TryParse(rest, out var n) && n > 0 ? n : 1);
        }
        else if (int.TryParse(value, out var number) && number != 0)
        {
            line = new GridLine(false, number);
        }
    }

    /// <summary>Parses "start / end"; a lone value is the start.</summary>
    public static void ParseLinePair(string value, out GridLine start, out GridLine end)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        ParseLine(parts[0], out start);
        if (parts.Length == 2) ParseLine(parts[1], out end);
        else end = GridLine.Auto;
    }
}

using System.Text;

namespace Sway.Core.Styling;

public readonly record struct Declaration(string Name, string Value, bool Important);

public sealed class StyleRule
{
    public required List<Selector> Selectors { get; init; }
    public required List<Declaration> Declarations { get; init; }
    public string? Media { get; init; }
}

/// <summary>One keyframe of an @keyframes rule: an offset (0..1) and the declarations that apply there.</summary>
public sealed record Keyframe(float Offset, List<Declaration> Declarations);

public sealed record KeyframesRule(string Name, List<Keyframe> Frames);

/// <summary>A font source: a file or URL, or an installed family named with local().</summary>
public readonly record struct FontSource(bool IsLocal, string Value);

public sealed record FontFaceRule(string Family, List<FontSource> Sources, int Weight, bool Italic);

public sealed class StyleSheet
{
    public List<StyleRule> Rules { get; } = new();
    public List<KeyframesRule> Keyframes { get; } = new();
    public List<FontFaceRule> FontFaces { get; } = new();
}

/// <summary>
/// Small CSS parser. It keeps declaration values as raw text so that custom properties and
/// <c>var()</c> survive until computed-value time, which a typed parser would drop.
/// </summary>
public static class CssParser
{
    public static StyleSheet Parse(string css)
    {
        var sheet = new StyleSheet();
        string text = StripComments(css);
        int i = 0;
        ParseRules(text, ref i, null, sheet, nested: false);
        return sheet;
    }

    public static List<Declaration> ParseDeclarations(string body)
    {
        var result = new List<Declaration>();
        int start = 0;
        foreach (int end in TopLevelSplits(body, ';'))
        {
            AddDeclaration(body.AsSpan(start, end - start), result);
            start = end + 1;
        }
        AddDeclaration(body.AsSpan(start), result);
        return result;
    }

    static void AddDeclaration(ReadOnlySpan<char> text, List<Declaration> into)
    {
        int colon = text.IndexOf(':');
        if (colon <= 0) return;

        string name = text[..colon].Trim().ToString();
        string value = text[(colon + 1)..].Trim().ToString();
        if (!name.StartsWith("--", StringComparison.Ordinal)) name = name.ToLowerInvariant();

        bool important = false;
        int bang = value.LastIndexOf('!');
        if (bang >= 0 && value[(bang + 1)..].Trim().Equals("important", StringComparison.OrdinalIgnoreCase))
        {
            important = true;
            value = value[..bang].TrimEnd();
        }

        if (name.Length > 0 && value.Length > 0) into.Add(new Declaration(name, value, important));
    }

    static void ParseRules(string s, ref int i, string? media, StyleSheet sheet, bool nested)
    {
        while (true)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return;

            if (nested && s[i] == '}') { i++; return; }

            if (s[i] == '@')
            {
                int stop = FindTopLevel(s, i, '{', ';');
                if (stop < 0) { i = s.Length; return; }

                string prelude = s.Substring(i + 1, stop - i - 1).Trim();
                if (s[stop] == ';') { i = stop + 1; continue; }

                int space = prelude.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '(' });
                string name = (space < 0 ? prelude : prelude[..space]).ToLowerInvariant();
                i = stop + 1;

                if (name == "media")
                {
                    string query = prelude[(space < 0 ? prelude.Length : space)..].Trim();
                    ParseRules(s, ref i, media is null ? query : $"{media} and {query}", sheet, nested: true);
                }
                else if (name == "supports")
                {
                    ParseRules(s, ref i, media, sheet, nested: true);
                }
                else if (name is "keyframes" or "-webkit-keyframes")
                {
                    string animationName = prelude[(space < 0 ? prelude.Length : space)..].Trim().Trim('"', '\'');
                    sheet.Keyframes.Add(new KeyframesRule(animationName, ParseKeyframes(s, ref i)));
                }
                else if (name == "font-face")
                {
                    i = stop;
                    int faceStart = i + 1;
                    SkipBlock(s, ref i);
                    var face = ParseFontFace(s.Substring(faceStart, Math.Max(0, i - 1 - faceStart)));
                    if (face is not null) sheet.FontFaces.Add(face);
                }
                else
                {
                    i = stop; // unsupported at-rule (@import, @layer, @container...): skip the block
                    SkipBlock(s, ref i);
                }
                continue;
            }

            int open = FindTopLevel(s, i, '{');
            if (open < 0) { i = s.Length; return; }

            string selectorText = s.Substring(i, open - i);
            i = open;
            int bodyStart = i + 1;
            SkipBlock(s, ref i);
            string body = s.Substring(bodyStart, Math.Max(0, i - 1 - bodyStart));

            var selectors = new List<Selector>();
            int from = 0;
            foreach (int comma in TopLevelSplits(selectorText, ','))
            {
                AddSelector(selectorText.Substring(from, comma - from), selectors);
                from = comma + 1;
            }
            AddSelector(selectorText.Substring(from), selectors);

            if (selectors.Count > 0)
                sheet.Rules.Add(new StyleRule { Selectors = selectors, Declarations = ParseDeclarations(body), Media = media });
        }
    }

    // Reads the frames inside an @keyframes block; i starts just after its opening brace.
    static List<Keyframe> ParseKeyframes(string s, ref int i)
    {
        var frames = new List<Keyframe>();
        while (true)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return frames;
            if (s[i] == '}') { i++; return frames; }

            int open = FindTopLevel(s, i, '{');
            if (open < 0) { i = s.Length; return frames; }

            string selectors = s.Substring(i, open - i);
            i = open;
            int bodyStart = i + 1;
            SkipBlock(s, ref i);
            var declarations = ParseDeclarations(s.Substring(bodyStart, Math.Max(0, i - 1 - bodyStart)));

            foreach (var part in selectors.Split(','))
            {
                string key = part.Trim().ToLowerInvariant();
                float? offset = key switch
                {
                    "from" => 0f,
                    "to" => 1f,
                    _ when key.EndsWith('%') && float.TryParse(key[..^1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct) && pct is >= 0 and <= 100 => pct / 100f,
                    _ => null
                };
                if (offset is { } o) frames.Add(new Keyframe(o, declarations));
            }
        }
    }

    static FontFaceRule? ParseFontFace(string body)
    {
        string? family = null;
        var sources = new List<FontSource>();
        int weight = 400;
        bool italic = false;

        foreach (var d in ParseDeclarations(body))
        {
            switch (d.Name)
            {
                case "font-family":
                    family = d.Value.Trim().Trim('"', '\'');
                    break;
                case "font-weight":
                    // A range like "100 900" describes a variable font; take its first value.
                    string first = d.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
                    weight = first switch { "normal" => 400, "bold" => 700, _ => int.TryParse(first, out var w) ? w : 400 };
                    break;
                case "font-style":
                    italic = d.Value.StartsWith("italic", StringComparison.OrdinalIgnoreCase) || d.Value.StartsWith("oblique", StringComparison.OrdinalIgnoreCase);
                    break;
                case "src":
                    foreach (var part in EffectValues.SplitTopLevel(d.Value, ','))
                    {
                        string p = part.Trim();
                        int open = p.IndexOf('(');
                        int close = p.IndexOf(')', open + 1);
                        if (open < 0 || close < 0) continue;

                        string fn = p[..open].ToLowerInvariant();
                        string arg = p[(open + 1)..close].Trim().Trim('"', '\'');
                        if (fn == "url") sources.Add(new FontSource(false, arg));
                        else if (fn == "local") sources.Add(new FontSource(true, arg));
                    }
                    break;
            }
        }
        return family is null || sources.Count == 0 ? null : new FontFaceRule(family, sources, weight, italic);
    }

    static void AddSelector(string text, List<Selector> into)
    {
        var selector = SelectorParser.Parse(text);
        if (selector is not null) into.Add(selector);
    }

    // Skips a balanced { } block; i must point at '{'. Leaves i just past the closing '}'.
    static void SkipBlock(string s, ref int i)
    {
        int depth = 0;
        char quote = '\0';
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) { i++; return; }
        }
    }

    // Index of the first stop char outside (), [] and quotes, or -1.
    static int FindTopLevel(string s, int start, params char[] stops)
    {
        int paren = 0;
        char quote = '\0';
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c is '(' or '[') paren++;
            else if (c is ')' or ']') paren--;
            else if (paren == 0 && Array.IndexOf(stops, c) >= 0) return i;
        }
        return -1;
    }

    internal static IEnumerable<int> TopLevelSplits(string s, char separator)
    {
        int paren = 0;
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c is '(' or '[') paren++;
            else if (c is ')' or ']') paren--;
            else if (paren == 0 && c == separator) yield return i;
        }
    }

    static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    static string StripComments(string css)
    {
        if (!css.Contains("/*", StringComparison.Ordinal)) return css;
        var sb = new StringBuilder(css.Length);
        for (int i = 0; i < css.Length; i++)
        {
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) break;
                i = end + 1;
            }
            else sb.Append(css[i]);
        }
        return sb.ToString();
    }
}

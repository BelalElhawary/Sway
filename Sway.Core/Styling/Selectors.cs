using Sway.Core.Dom;

namespace Sway.Core.Styling;

public enum Combinator { None, Descendant, Child, NextSibling, SubsequentSibling }

public enum AttrOp { Exists, Equals, Includes, DashMatch, Prefix, Suffix, Substring }

public sealed record AttrSelector(string Name, AttrOp Op, string Value, bool IgnoreCase);

public sealed class PseudoClass
{
    public required string Name { get; init; }
    public List<Selector>? Inner { get; init; }
    public int A { get; init; }
    public int B { get; init; }
}

public sealed class Compound
{
    public string? Tag { get; set; }
    public string? Id { get; set; }
    public List<string> Classes { get; } = new();
    public List<AttrSelector> Attributes { get; } = new();
    public List<PseudoClass> Pseudos { get; } = new();
}

public sealed class SelectorPart
{
    /// <summary>How this part relates to the previous (left) part.</summary>
    public Combinator Combinator { get; init; }
    public required Compound Compound { get; init; }
}

public sealed class Selector
{
    public List<SelectorPart> Parts { get; } = new();
    public int Specificity { get; set; }
}

public static class SelectorParser
{
    /// <summary>Returns null for selectors this engine does not support (e.g. pseudo-elements).</summary>
    public static Selector? Parse(string text)
    {
        try { return new Parser(text.Trim()).ParseSelector(); }
        catch (FormatException) { return null; }
    }

    sealed class Parser
    {
        readonly string _s;
        int _i;

        public Parser(string s) => _s = s;

        public Selector ParseSelector()
        {
            var selector = new Selector();
            var combinator = Combinator.None;
            int a = 0, b = 0, c = 0;

            while (true)
            {
                bool sawWhitespace = SkipWhitespace();
                if (_i >= _s.Length) break;

                char ch = _s[_i];
                if (ch is '>' or '+' or '~')
                {
                    combinator = ch switch { '>' => Combinator.Child, '+' => Combinator.NextSibling, _ => Combinator.SubsequentSibling };
                    _i++;
                    SkipWhitespace();
                }
                else if (selector.Parts.Count > 0 && sawWhitespace)
                {
                    combinator = Combinator.Descendant;
                }

                var compound = ParseCompound(ref a, ref b, ref c);
                selector.Parts.Add(new SelectorPart { Combinator = selector.Parts.Count == 0 ? Combinator.None : combinator, Compound = compound });
                combinator = Combinator.None;
            }

            if (selector.Parts.Count == 0) throw new FormatException("empty selector");
            selector.Specificity = (a << 16) | (b << 8) | c;
            return selector;
        }

        Compound ParseCompound(ref int a, ref int b, ref int c)
        {
            var compound = new Compound();
            int start = _i;

            while (_i < _s.Length)
            {
                char ch = _s[_i];
                if (char.IsWhiteSpace(ch) || ch is '>' or '+' or '~' or ',') break;

                switch (ch)
                {
                    case '*':
                        _i++;
                        break;
                    case '#':
                        _i++;
                        compound.Id = ReadIdent();
                        a++;
                        break;
                    case '.':
                        _i++;
                        compound.Classes.Add(ReadIdent());
                        b++;
                        break;
                    case '[':
                        compound.Attributes.Add(ParseAttribute());
                        b++;
                        break;
                    case ':':
                        ParsePseudo(compound, ref a, ref b, ref c);
                        break;
                    default:
                        compound.Tag = ReadIdent().ToLowerInvariant();
                        c++;
                        break;
                }
            }

            if (_i == start) throw new FormatException("expected a selector");
            return compound;
        }

        void ParsePseudo(Compound compound, ref int a, ref int b, ref int c)
        {
            _i++;
            if (_i < _s.Length && _s[_i] == ':') throw new FormatException("pseudo-elements are not supported");

            string name = ReadIdent().ToLowerInvariant();
            string? arg = null;
            if (_i < _s.Length && _s[_i] == '(')
            {
                int close = FindClosingParen(_i);
                arg = _s.Substring(_i + 1, close - _i - 1);
                _i = close + 1;
            }

            switch (name)
            {
                case "hover" or "active" or "focus" or "focus-visible" or "disabled" or "enabled" or "checked" or "indeterminate"
                    or "first-child" or "last-child" or "only-child" or "root" or "empty":
                    b++;
                    compound.Pseudos.Add(new PseudoClass { Name = name });
                    break;
                case "nth-child":
                {
                    var (pa, pb) = ParseNth(arg ?? throw new FormatException("nth-child needs an argument"));
                    b++;
                    compound.Pseudos.Add(new PseudoClass { Name = name, A = pa, B = pb });
                    break;
                }
                case "not" or "is" or "where":
                {
                    var inner = new List<Selector>();
                    string list = arg ?? throw new FormatException(":" + name + " needs an argument");
                    int from = 0;
                    foreach (int comma in CssParser.TopLevelSplits(list, ','))
                    {
                        inner.Add(new Parser(list.Substring(from, comma - from).Trim()).ParseSelector());
                        from = comma + 1;
                    }
                    inner.Add(new Parser(list.Substring(from).Trim()).ParseSelector());

                    if (name != "where")
                    {
                        int max = inner.Max(s => s.Specificity);
                        a += max >> 16; b += (max >> 8) & 0xFF; c += max & 0xFF;
                    }
                    compound.Pseudos.Add(new PseudoClass { Name = name, Inner = inner });
                    break;
                }
                default:
                    throw new FormatException("unsupported pseudo-class :" + name);
            }
        }

        AttrSelector ParseAttribute()
        {
            int close = _s.IndexOf(']', _i);
            if (close < 0) throw new FormatException("unterminated attribute selector");
            string body = _s.Substring(_i + 1, close - _i - 1).Trim();
            _i = close + 1;

            bool ignoreCase = false;
            if (body.EndsWith(" i", StringComparison.OrdinalIgnoreCase)) { ignoreCase = true; body = body[..^2].TrimEnd(); }

            int eq = body.IndexOf('=');
            if (eq < 0) return new AttrSelector(body, AttrOp.Exists, "", false);

            char opChar = eq > 0 ? body[eq - 1] : '\0';
            var op = opChar switch
            {
                '~' => AttrOp.Includes, '|' => AttrOp.DashMatch, '^' => AttrOp.Prefix,
                '$' => AttrOp.Suffix, '*' => AttrOp.Substring, _ => AttrOp.Equals
            };
            string name = (op == AttrOp.Equals ? body[..eq] : body[..(eq - 1)]).Trim();
            string value = body[(eq + 1)..].Trim().Trim('"', '\'');
            return new AttrSelector(name, op, value, ignoreCase);
        }

        static (int a, int b) ParseNth(string arg)
        {
            arg = arg.Trim().ToLowerInvariant().Replace(" ", "");
            if (arg == "odd") return (2, 1);
            if (arg == "even") return (2, 0);

            int n = arg.IndexOf('n');
            if (n < 0) return (0, int.Parse(arg));

            string aText = arg[..n];
            int a = aText switch { "" or "+" => 1, "-" => -1, _ => int.Parse(aText) };
            string bText = arg[(n + 1)..];
            return (a, bText.Length == 0 ? 0 : int.Parse(bText));
        }

        int FindClosingParen(int open)
        {
            int depth = 0;
            for (int i = open; i < _s.Length; i++)
            {
                if (_s[i] == '(') depth++;
                else if (_s[i] == ')' && --depth == 0) return i;
            }
            throw new FormatException("unbalanced parentheses");
        }

        string ReadIdent()
        {
            int start = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] is '-' or '_' || _s[_i] > 127)) _i++;
            if (_i == start) throw new FormatException("expected identifier");
            return _s.Substring(start, _i - start);
        }

        bool SkipWhitespace()
        {
            int start = _i;
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            return _i > start;
        }
    }
}

public static class SelectorMatcher
{
    public static bool Matches(Selector selector, ElementNode element) =>
        MatchFrom(selector, selector.Parts.Count - 1, element);

    static bool MatchFrom(Selector selector, int index, ElementNode element)
    {
        var part = selector.Parts[index];
        if (!MatchCompound(part.Compound, element)) return false;
        if (index == 0) return true;

        switch (part.Combinator)
        {
            case Combinator.Child:
                return element.ParentElement is { } parent && MatchFrom(selector, index - 1, parent);
            case Combinator.Descendant:
                for (var p = element.ParentElement; p is not null; p = p.ParentElement)
                    if (MatchFrom(selector, index - 1, p)) return true;
                return false;
            case Combinator.NextSibling:
                return element.PreviousSiblingElement() is { } prev && MatchFrom(selector, index - 1, prev);
            case Combinator.SubsequentSibling:
                for (var s = element.PreviousSiblingElement(); s is not null; s = s.PreviousSiblingElement())
                    if (MatchFrom(selector, index - 1, s)) return true;
                return false;
            default:
                return false;
        }
    }

    public static bool IsStatePseudo(string name) => name is "hover" or "active" or "focus" or "focus-visible";

    /// <summary>Does the element satisfy everything about this compound except the state pseudo-classes?</summary>
    public static bool MatchesIgnoringState(Compound compound, ElementNode element) => MatchCompound(compound, element, ignoreState: true);

    static bool MatchCompound(Compound compound, ElementNode element, bool ignoreState = false)
    {
        if (compound.Tag is not null && compound.Tag != element.Tag) return false;
        if (compound.Id is not null && compound.Id != element.Id) return false;

        foreach (var cls in compound.Classes)
        {
            bool found = false;
            foreach (var c in element.Classes) if (c == cls) { found = true; break; }
            if (!found) return false;
        }

        foreach (var attr in compound.Attributes)
            if (!MatchAttribute(attr, element)) return false;

        foreach (var pseudo in compound.Pseudos)
        {
            if (ignoreState && IsStatePseudo(pseudo.Name)) continue;
            if (!MatchPseudo(pseudo, element)) return false;
        }

        return true;
    }

    static bool MatchAttribute(AttrSelector attr, ElementNode element)
    {
        var actual = element.GetAttribute(attr.Name);
        if (actual is null) return false;
        if (attr.Op == AttrOp.Exists) return true;

        var cmp = attr.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return attr.Op switch
        {
            AttrOp.Equals => actual.Equals(attr.Value, cmp),
            AttrOp.Includes => actual.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(w => w.Equals(attr.Value, cmp)),
            AttrOp.DashMatch => actual.Equals(attr.Value, cmp) || actual.StartsWith(attr.Value + "-", cmp),
            AttrOp.Prefix => attr.Value.Length > 0 && actual.StartsWith(attr.Value, cmp),
            AttrOp.Suffix => attr.Value.Length > 0 && actual.EndsWith(attr.Value, cmp),
            AttrOp.Substring => attr.Value.Length > 0 && actual.Contains(attr.Value, cmp),
            _ => false
        };
    }

    static bool MatchPseudo(PseudoClass pseudo, ElementNode element)
    {
        switch (pseudo.Name)
        {
            case "hover": return element.State.HasFlag(ElementState.Hover);
            case "active": return element.State.HasFlag(ElementState.Active);
            case "focus": return element.State.HasFlag(ElementState.Focus);
            case "focus-visible": return element.State.HasFlag(ElementState.FocusVisible);
            case "disabled": return element.IsDisabled;
            case "enabled": return !element.IsDisabled;
            case "checked": return element.Attributes.ContainsKey("checked");
            case "indeterminate": return element.Attributes.ContainsKey("indeterminate");
            case "root": return element.Parent is null;
            case "empty": return element.PhysicalChildren.Count == 0;
            case "first-child": return element.ElementPosition().index == 1;
            case "last-child": { var (i, n) = element.ElementPosition(); return i == n; }
            case "only-child": return element.ElementPosition().count == 1;
            case "nth-child":
            {
                int position = element.ElementPosition().index;
                if (pseudo.A == 0) return position == pseudo.B;
                int n = position - pseudo.B;
                return n % pseudo.A == 0 && n / pseudo.A >= 0;
            }
            case "not": return !pseudo.Inner!.Any(s => Matches(s, element));
            case "is" or "where": return pseudo.Inner!.Any(s => Matches(s, element));
            default: return false;
        }
    }
}

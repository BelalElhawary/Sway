using System.Globalization;
using SkiaSharp;
using Sway.Core.Dom;

namespace Sway.Core.Styling;

public enum ColorScheme { Light, Dark }

/// <summary>
/// Matches selectors, runs the cascade and produces a <see cref="ComputedStyle"/> per element.
/// Rules are bucketed by the key of their rightmost compound (id, class, tag) so an element
/// only tests selectors that could plausibly match.
/// </summary>
public sealed class StyleResolver : IStyleInvalidation
{
    readonly record struct Entry(StyleRule Rule, Selector Selector, int Order);
    readonly record struct Candidate(Declaration Declaration, int Specificity, int Order);

    readonly Dictionary<string, List<Entry>> _byId = new();
    readonly Dictionary<string, List<Entry>> _byClass = new();
    readonly Dictionary<string, List<Entry>> _byTag = new();
    readonly List<Entry> _universal = new();
    readonly Dictionary<string, KeyframesRule> _keyframes = new();
    readonly Dictionary<ElementState, List<Compound>> _stateCompounds = new();
    readonly Dictionary<(string, float, float, float), TrackTemplate> _templates = new();
    bool _stateConservative;
    int _order;

    public StyleResolver()
    {
        Animator = new Animator(this);
        AddStyleSheet(CssParser.Parse(UserAgentStyles.Css));
    }

    public Animator Animator { get; }

    /// <summary>True when some rule uses the + or ~ combinators, so a change can affect following siblings.</summary>
    public bool HasSiblingSelectors { get; private set; }

    /// <summary>
    /// Can a state flip on this element change anyone's style? True only if some selector has the matching
    /// pseudo-class on a compound this element satisfies, which keeps hovering a deep element from restyling its ancestors.
    /// </summary>
    public bool StateMayAffect(ElementNode element, ElementState flag)
    {
        if (_stateConservative) return true;
        if (!_stateCompounds.TryGetValue(flag, out var compounds)) return false;

        foreach (var compound in compounds)
            if (SelectorMatcher.MatchesIgnoringState(compound, element)) return true;
        return false;
    }

    void IndexStateDependencies(Selector selector)
    {
        foreach (var part in selector.Parts)
        {
            if (part.Combinator is Combinator.NextSibling or Combinator.SubsequentSibling) HasSiblingSelectors = true;

            foreach (var pseudo in part.Compound.Pseudos)
            {
                if (SelectorMatcher.IsStatePseudo(pseudo.Name))
                {
                    var flag = pseudo.Name switch
                    {
                        "hover" => ElementState.Hover, "active" => ElementState.Active,
                        "focus" => ElementState.Focus, _ => ElementState.FocusVisible
                    };
                    if (!_stateCompounds.TryGetValue(flag, out var list)) _stateCompounds[flag] = list = new List<Compound>();
                    list.Add(part.Compound);
                }
                // State inside :not() / :is() is too indirect to track precisely, so fall back to broad invalidation.
                if (pseudo.Inner is not null && pseudo.Inner.Any(InnerUsesState)) _stateConservative = true;
            }
        }
    }

    static bool InnerUsesState(Selector selector) =>
        selector.Parts.Any(p => p.Compound.Pseudos.Any(ps => SelectorMatcher.IsStatePseudo(ps.Name) || (ps.Inner?.Any(InnerUsesState) ?? false)));

    bool TryTemplate(string value, float fontSize, StyleContext ctx, out TrackTemplate template)
    {
        // Identical declarations share one template object, which lets style comparison use reference equality.
        var key = (value, fontSize, ctx.ViewportWidth, ctx.ViewportHeight);
        if (_templates.TryGetValue(key, out template!)) return true;
        if (!GridValues.TryParseTemplate(value, fontSize, ctx, out template!)) return false;
        _templates[key] = template;
        return true;
    }

    /// <summary>The clock (in ms) transitions and animations measure from; set once per frame.</summary>
    public double NowMs { get; set; }

    public KeyframesRule? FindKeyframes(string name) => _keyframes.TryGetValue(name, out var rule) ? rule : null;

    /// <summary>Applies one declaration to a style, as the cascade does. Used to evaluate keyframes.</summary>
    internal void ApplyDeclaration(ComputedStyle style, ComputedStyle? parent, Declaration declaration) =>
        Apply(style, parent, declaration);

    public StyleContext Context { get; set; } = new(800, 600);
    public ColorScheme ColorScheme { get; set; } = ColorScheme.Light;

    public void AddStyleSheet(StyleSheet sheet)
    {
        foreach (var keyframes in sheet.Keyframes) _keyframes[keyframes.Name] = keyframes; // later rules win

        foreach (var rule in sheet.Rules)
        {
            foreach (var selector in rule.Selectors)
            {
                IndexStateDependencies(selector);
                var entry = new Entry(rule, selector, _order++);
                var key = selector.Parts[^1].Compound;
                if (key.Id is not null) Bucket(_byId, key.Id).Add(entry);
                else if (key.Classes.Count > 0) Bucket(_byClass, key.Classes[0]).Add(entry);
                else if (key.Tag is not null) Bucket(_byTag, key.Tag).Add(entry);
                else _universal.Add(entry);
            }
        }
    }

    static List<Entry> Bucket(Dictionary<string, List<Entry>> map, string key)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<Entry>();
        return list;
    }

    public void Resolve(Document document)
    {
        var root = document.Root;

        if (document.FullStyleDirty || !root.HasComputedStyle)
        {
            Restyle(root, null);
            ResolveChildren(root);
        }
        else
        {
            // Only the subtrees under the invalidated elements need new styles. A root whose ancestor is also
            // invalidated is covered by that ancestor.
            var roots = document.StyleRoots.ToList();
            var rootSet = new HashSet<ElementNode>(roots);
            foreach (var r in roots)
            {
                if (!IsAttached(document, r) || HasInvalidatedAncestor(r, rootSet)) continue;
                Restyle(r, r.ParentElement?.Style);
                ResolveChildren(r);
            }
        }

        document.StyleRoots.Clear();
        document.StyleDirty = false;
        document.FullStyleDirty = false;
    }

    static bool IsAttached(Document document, ElementNode element)
    {
        Node node = element;
        while (node.Parent is { } parent) node = parent;
        return node == document.Root;
    }

    static bool HasInvalidatedAncestor(ElementNode element, HashSet<ElementNode> roots)
    {
        for (var a = element.ParentElement; a is not null; a = a.ParentElement)
            if (roots.Contains(a)) return true;
        return false;
    }

    void ResolveChildren(ElementNode element)
    {
        foreach (var child in element.PhysicalChildren)
        {
            if (child is not ElementNode e) continue;
            Restyle(e, element.Style);
            ResolveChildren(e);
        }
    }

    void Restyle(ElementNode element, ComputedStyle? parent)
    {
        var old = element.HasComputedStyle ? element.Style : null;
        var fresh = ComputeStyle(element, parent);
        Animator.OnRestyle(element, old, fresh, NowMs);

        // A restyle only costs a relayout if it changed something layout reads.
        if (old is null || !old.LayoutEquals(fresh)) element.MarkLayoutDirty();

        element.Style = fresh;
        element.HasComputedStyle = true;

        // Positioned and composited boxes are painted out of normal flow; let ancestors know one exists below them.
        if (fresh.Position != Position.Static || fresh.Opacity < 1 || fresh.Transform is not null || fresh.Filters.Count > 0)
            for (var a = element.ParentElement; a is not null && !a.HasEntryDescendant; a = a.ParentElement)
                a.HasEntryDescendant = true;
    }

    ComputedStyle ComputeStyle(ElementNode element, ComputedStyle? parent)
    {
        var candidates = new List<Candidate>();
        Collect(element, candidates);

        if (element.GetAttribute("style") is { } inline)
            foreach (var d in CssParser.ParseDeclarations(inline))
                candidates.Add(new Candidate(d, int.MaxValue, int.MaxValue));

        // Normal declarations first, then !important ones, each ordered by specificity then source order.
        var ordered = candidates
            .OrderBy(c => c.Declaration.Important ? 1 : 0)
            .ThenBy(c => c.Specificity)
            .ThenBy(c => c.Order)
            .Select(c => c.Declaration)
            .ToList();

        var style = parent is null ? ComputedStyle.CreateRoot() : ComputedStyle.CreateRoot().InheritFrom(parent);

        // Custom properties inherit by copy-on-write so siblings share the parent's dictionary.
        if (ordered.Any(d => d.Name.StartsWith("--", StringComparison.Ordinal)))
        {
            style.CustomProperties = new Dictionary<string, string>(style.CustomProperties);
            foreach (var d in ordered)
                if (d.Name.StartsWith("--", StringComparison.Ordinal)) style.CustomProperties[d.Name] = d.Value;
        }

        // Font size and color go first: em units and currentColor depend on them.
        foreach (var d in ordered)
            if (d.Name is "font-size" or "color") Apply(style, parent, d);
        foreach (var d in ordered)
            if (d.Name is not ("font-size" or "color") && !d.Name.StartsWith("--", StringComparison.Ordinal)) Apply(style, parent, d);

        // Out-of-flow boxes are blockified, and a clipped axis forces the other axis to clip too.
        if (style.IsOutOfFlow && style.Display is Display.Inline or Display.InlineBlock) style.Display = Display.Block;
        if (style.OverflowX == Overflow.Visible && style.OverflowY != Overflow.Visible) style.OverflowX = Overflow.Auto;
        if (style.OverflowY == Overflow.Visible && style.OverflowX != Overflow.Visible) style.OverflowY = Overflow.Auto;

        return style;
    }

    void Collect(ElementNode element, List<Candidate> into)
    {
        if (element.Id is { } id && _byId.TryGetValue(id, out var byId)) Test(byId, element, into);
        foreach (var cls in element.Classes)
            if (_byClass.TryGetValue(cls, out var byClass)) Test(byClass, element, into);
        if (_byTag.TryGetValue(element.Tag, out var byTag)) Test(byTag, element, into);
        Test(_universal, element, into);
    }

    void Test(List<Entry> entries, ElementNode element, List<Candidate> into)
    {
        foreach (var entry in entries)
        {
            if (entry.Rule.Media is not null && !MediaMatches(entry.Rule.Media)) continue;
            if (!SelectorMatcher.Matches(entry.Selector, element)) continue;
            foreach (var d in entry.Rule.Declarations)
                into.Add(new Candidate(d, entry.Selector.Specificity, entry.Order));
        }
    }

    bool MediaMatches(string query)
    {
        foreach (var alternative in query.Split(','))
        {
            bool all = true;
            foreach (var term in alternative.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!TermMatches(term)) { all = false; break; }
            if (all) return true;
        }
        return false;
    }

    bool TermMatches(string term)
    {
        term = term.Trim();
        if (term is "screen" or "all") return true;
        if (!term.StartsWith('(') || !term.EndsWith(')')) return false;

        var parts = term[1..^1].Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        switch (parts[0].ToLowerInvariant())
        {
            case "prefers-color-scheme":
                return parts[1].Equals(ColorScheme.ToString(), StringComparison.OrdinalIgnoreCase);
            case "min-width" or "max-width" or "min-height" or "max-height":
                if (!CssValues.TryLength(parts[1], 16, Context, out var len) || len.Unit != LengthUnit.Px) return false;
                float size = parts[0].EndsWith("width") ? Context.ViewportWidth : Context.ViewportHeight;
                return parts[0].StartsWith("min") ? size >= len.Value : size <= len.Value;
            default:
                return false;
        }
    }

    // ---- property application ----

    void Apply(ComputedStyle s, ComputedStyle? parent, Declaration d)
    {
        string? value = CssValues.ResolveVars(d.Value, s.CustomProperties);
        if (value is null) return;
        value = value.Trim();

        if (value.Equals("inherit", StringComparison.OrdinalIgnoreCase) && parent is not null)
        {
            Inherit(s, parent, d.Name);
            return;
        }

        float fs = s.FontSize;
        var ctx = Context;

        switch (d.Name)
        {
            case "display":
                s.Display = value.ToLowerInvariant() switch
                {
                    "none" => Display.None, "block" or "list-item" => Display.Block, "inline" => Display.Inline,
                    "inline-block" => Display.InlineBlock, "flex" => Display.Flex, "inline-flex" => Display.InlineFlex,
                    "grid" => Display.Grid, "inline-grid" => Display.InlineGrid,
                    _ => s.Display
                };
                break;
            case "color":
                if (CssValues.TryColor(value, parent?.Color ?? SKColors.Black, out var color)) s.Color = color;
                break;
            case "background-color":
                if (CssValues.TryColor(value, s.Color, out var bg)) s.BackgroundColor = bg;
                break;
            case "background":
                // The shorthand resets the layers it covers; position, size and repeat are not supported yet.
                s.BackgroundColor = SKColors.Transparent;
                s.BackgroundGradients = EffectValues.ParseGradients(value, fs, ctx, s.Color);
                foreach (var layer in EffectValues.SplitTopLevel(value, ','))
                    foreach (var token in CssValues.SplitTokens(layer))
                        if (CssValues.TryColor(token, s.Color, out var bgc)) s.BackgroundColor = bgc;
                break;
            case "opacity":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var op)) s.Opacity = Math.Clamp(op, 0, 1);
                break;
            case "font-size":
                if (value.EndsWith('%') && float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                    s.FontSize = (parent?.FontSize ?? 16) * pct / 100f;
                else if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase) && !value.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                        s.FontSize = (parent?.FontSize ?? 16) * em;
                }
                else if (CssValues.TryLength(value, parent?.FontSize ?? 16, ctx, out var size) && size.Unit == LengthUnit.Px)
                    s.FontSize = size.Value;
                break;
            case "font-weight":
                s.FontWeight = value.ToLowerInvariant() switch
                {
                    "normal" => 400, "bold" => 700, "lighter" => 300, "bolder" => 800,
                    _ => int.TryParse(value, out var w) ? Math.Clamp(w, 1, 1000) : s.FontWeight
                };
                break;
            case "font-style":
                s.Italic = value.Equals("italic", StringComparison.OrdinalIgnoreCase) || value.Equals("oblique", StringComparison.OrdinalIgnoreCase);
                break;
            case "font-family":
                s.FontFamily = value;
                break;
            case "line-height":
                if (value.Equals("normal", StringComparison.OrdinalIgnoreCase)) s.LineHeight = null;
                else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult)) { s.LineHeight = mult; s.LineHeightIsMultiplier = true; }
                else if (CssValues.TryLength(value, fs, ctx, out var lh) && lh.Unit == LengthUnit.Px) { s.LineHeight = lh.Value; s.LineHeightIsMultiplier = false; }
                break;
            case "text-align":
                s.TextAlign = value.ToLowerInvariant() switch { "center" => TextAlign.Center, "right" or "end" => TextAlign.Right, _ => TextAlign.Left };
                break;
            case "box-sizing":
                s.BorderBox = value.Equals("border-box", StringComparison.OrdinalIgnoreCase);
                break;

            case "width": SetLength(value, fs, v => s.Width = v); break;
            case "height": SetLength(value, fs, v => s.Height = v); break;
            case "min-width": SetLength(value, fs, v => s.MinWidth = v); break;
            case "min-height": SetLength(value, fs, v => s.MinHeight = v); break;
            case "max-width": SetLength(value, fs, v => s.MaxWidth = v); break;
            case "max-height": SetLength(value, fs, v => s.MaxHeight = v); break;

            case "margin": SetBox(value, fs, s.Margin); break;
            case "padding": SetBox(value, fs, s.Padding); break;
            case "margin-top": SetLength(value, fs, v => s.Margin[ComputedStyle.Top] = v); break;
            case "margin-right": SetLength(value, fs, v => s.Margin[ComputedStyle.Right] = v); break;
            case "margin-bottom": SetLength(value, fs, v => s.Margin[ComputedStyle.Bottom] = v); break;
            case "margin-left": SetLength(value, fs, v => s.Margin[ComputedStyle.Left] = v); break;
            case "padding-top": SetLength(value, fs, v => s.Padding[ComputedStyle.Top] = v); break;
            case "padding-right": SetLength(value, fs, v => s.Padding[ComputedStyle.Right] = v); break;
            case "padding-bottom": SetLength(value, fs, v => s.Padding[ComputedStyle.Bottom] = v); break;
            case "padding-left": SetLength(value, fs, v => s.Padding[ComputedStyle.Left] = v); break;

            case "position":
                s.Position = value.ToLowerInvariant() switch
                {
                    "relative" => Position.Relative, "absolute" => Position.Absolute,
                    "fixed" => Position.Fixed, "sticky" => Position.Sticky, _ => Position.Static
                };
                break;
            case "top": SetLength(value, fs, v => s.Inset[ComputedStyle.Top] = v); break;
            case "right": SetLength(value, fs, v => s.Inset[ComputedStyle.Right] = v); break;
            case "bottom": SetLength(value, fs, v => s.Inset[ComputedStyle.Bottom] = v); break;
            case "left": SetLength(value, fs, v => s.Inset[ComputedStyle.Left] = v); break;
            case "inset": SetBox(value, fs, s.Inset); break;
            case "z-index":
                if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) s.ZIndex = null;
                else if (int.TryParse(value, out var z)) s.ZIndex = z;
                break;
            case "overflow":
            {
                var tokens = CssValues.SplitTokens(value);
                if (tokens.Count is 1 or 2)
                {
                    s.OverflowX = ParseOverflow(tokens[0], s.OverflowX);
                    s.OverflowY = ParseOverflow(tokens[^1], s.OverflowY);
                }
                break;
            }
            case "overflow-x": s.OverflowX = ParseOverflow(value, s.OverflowX); break;
            case "overflow-y": s.OverflowY = ParseOverflow(value, s.OverflowY); break;
            case "outline": ApplyOutlineShorthand(s, value, fs); break;
            case "outline-width": if (TryPx(value, fs, out var ow)) s.OutlineWidth = ow; break;
            case "outline-offset": if (TryPx(value, fs, out var oo)) s.OutlineOffset = oo; break;
            case "outline-color": if (CssValues.TryColor(value, s.Color, out var oc)) s.OutlineColor = oc; break;
            case "outline-style":
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) s.OutlineWidth = 0;
                break;
            case "text-decoration" or "text-decoration-line":
            {
                var tokens = CssValues.SplitTokens(value.ToLowerInvariant());
                var deco = TextDecoration.None;
                if (tokens.Contains("underline")) deco |= TextDecoration.Underline;
                if (tokens.Contains("line-through")) deco |= TextDecoration.LineThrough;
                s.TextDecoration = deco;
                break;
            }
            case "accent-color":
                if (CssValues.TryColor(value, s.Color, out var accent)) s.AccentColor = accent;
                break;
            case "cursor":
                s.Cursor = value.Split(',')[^1].Trim().ToLowerInvariant();
                break;
            case "visibility": s.VisibilityHidden = value is "hidden" or "collapse"; break;
            case "pointer-events": s.PointerEventsNone = value.Equals("none", StringComparison.OrdinalIgnoreCase); break;

            case "transition": ApplyTransitionShorthand(s, value); break;
            case "transition-property": s.TransitionProperty = CommaList(value).Select(v => v.ToLowerInvariant()).ToList(); break;
            case "transition-duration": SetTimes(value, list => s.TransitionDuration = list); break;
            case "transition-delay": SetTimes(value, list => s.TransitionDelay = list); break;
            case "transition-timing-function": SetTimings(value, list => s.TransitionTiming = list); break;

            case "animation": ApplyAnimationShorthand(s, value); break;
            case "animation-name": s.AnimationName = CommaList(value); break;
            case "animation-duration": SetTimes(value, list => s.AnimationDuration = list); break;
            case "animation-delay": SetTimes(value, list => s.AnimationDelay = list); break;
            case "animation-timing-function": SetTimings(value, list => s.AnimationTiming = list); break;
            case "animation-iteration-count":
                s.AnimationIterations = CommaList(value).Select(v => v.Equals("infinite", StringComparison.OrdinalIgnoreCase)
                    ? float.PositiveInfinity : TryNumber(v, out var n) ? n : 1f).ToList();
                break;
            case "animation-direction":
                s.AnimationDirections = CommaList(value).Select(ParseAnimationDirection).ToList();
                break;
            case "animation-fill-mode":
                s.AnimationFills = CommaList(value).Select(ParseAnimationFill).ToList();
                break;
            case "animation-play-state":
                s.AnimationPaused = CommaList(value).Select(v => v.Equals("paused", StringComparison.OrdinalIgnoreCase)).ToList();
                break;

            case "flex-direction": s.FlexDirection = ParseDirection(value); break;
            case "flex-wrap": s.FlexWrap = value.StartsWith("wrap", StringComparison.OrdinalIgnoreCase); break;
            case "flex-flow":
                foreach (var token in CssValues.SplitTokens(value.ToLowerInvariant()))
                {
                    if (token.StartsWith("wrap")) s.FlexWrap = true;
                    else if (token == "nowrap") s.FlexWrap = false;
                    else s.FlexDirection = ParseDirection(token);
                }
                break;
            case "flex-grow": if (TryNumber(value, out var grow)) s.FlexGrow = Math.Max(0, grow); break;
            case "flex-shrink": if (TryNumber(value, out var shrink)) s.FlexShrink = Math.Max(0, shrink); break;
            case "flex-basis": SetLength(value, fs, v => s.FlexBasis = v); break;
            case "flex": ApplyFlexShorthand(s, value, fs); break;
            case "order": if (int.TryParse(value, out var order)) s.Order = order; break;

            case "justify-content": s.JustifyContent = ParseAlign(value, s.JustifyContent); break;
            case "align-content": s.AlignContent = ParseAlign(value, s.AlignContent); break;
            case "align-items": s.AlignItems = ParseAlign(value, s.AlignItems); break;
            case "align-self": s.AlignSelf = ParseAlign(value, s.AlignSelf); break;
            case "justify-items": s.JustifyItems = ParseAlign(value, s.JustifyItems); break;
            case "justify-self": s.JustifySelf = ParseAlign(value, s.JustifySelf); break;

            case "place-content":
            {
                var (a, j) = SplitPlaceValue(value);
                s.AlignContent = ParseAlign(a, s.AlignContent);
                s.JustifyContent = ParseAlign(j, s.JustifyContent);
                break;
            }
            case "place-items":
            {
                var (a, j) = SplitPlaceValue(value);
                s.AlignItems = ParseAlign(a, s.AlignItems);
                s.JustifyItems = ParseAlign(j, s.JustifyItems);
                break;
            }
            case "place-self":
            {
                var (a, j) = SplitPlaceValue(value);
                s.AlignSelf = ParseAlign(a, s.AlignSelf);
                s.JustifySelf = ParseAlign(j, s.JustifySelf);
                break;
            }

            case "gap" or "grid-gap":
            {
                var tokens = CssValues.SplitTokens(value);
                if (tokens.Count is >= 1 and <= 2
                    && TryPx(tokens[0], fs, out var rowGap) && TryPx(tokens[^1], fs, out var colGap))
                {
                    s.RowGap = rowGap;
                    s.ColumnGap = colGap;
                }
                break;
            }
            case "row-gap" or "grid-row-gap": if (TryPx(value, fs, out var rg)) s.RowGap = rg; break;
            case "column-gap" or "grid-column-gap": if (TryPx(value, fs, out var cg)) s.ColumnGap = cg; break;

            case "grid-template-columns": if (TryTemplate(value, fs, ctx, out var cols)) s.GridColumns = cols; break;
            case "grid-template-rows": if (TryTemplate(value, fs, ctx, out var rows)) s.GridRows = rows; break;
            case "grid-auto-columns": if (GridValues.TryParseTrack(value, fs, ctx, out var ac)) s.GridAutoColumns = ac; break;
            case "grid-auto-rows": if (GridValues.TryParseTrack(value, fs, ctx, out var ar)) s.GridAutoRows = ar; break;
            case "grid-column": GridValues.ParseLinePair(value, out s.ColumnStart, out s.ColumnEnd); break;
            case "grid-row": GridValues.ParseLinePair(value, out s.RowStart, out s.RowEnd); break;
            case "grid-column-start": GridValues.ParseLine(value, out s.ColumnStart); break;
            case "grid-column-end": GridValues.ParseLine(value, out s.ColumnEnd); break;
            case "grid-row-start": GridValues.ParseLine(value, out s.RowStart); break;
            case "grid-row-end": GridValues.ParseLine(value, out s.RowEnd); break;

            case "border": ApplyBorderShorthand(s, value, all: true, side: -1, fs); break;
            case "border-top": ApplyBorderShorthand(s, value, false, ComputedStyle.Top, fs); break;
            case "border-right": ApplyBorderShorthand(s, value, false, ComputedStyle.Right, fs); break;
            case "border-bottom": ApplyBorderShorthand(s, value, false, ComputedStyle.Bottom, fs); break;
            case "border-left": ApplyBorderShorthand(s, value, false, ComputedStyle.Left, fs); break;
            case "border-width":
            {
                var widths = new Length[4];
                if (ExpandBox(value, fs, widths))
                    for (int i = 0; i < 4; i++) s.BorderWidth[i] = widths[i].Value;
                break;
            }
            case "border-color":
            {
                var tokens = CssValues.SplitTokens(value);
                for (int i = 0; i < 4; i++)
                {
                    string t = BoxToken(tokens, i);
                    if (CssValues.TryColor(t, s.Color, out var bc)) s.BorderColor[i] = bc;
                }
                break;
            }
            case "border-style":
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                    Array.Clear(s.BorderWidth);
                break;
            case "border-radius":
            {
                // "a b c d" maps to top-left, top-right, bottom-right, bottom-left; elliptical "/" radii are not supported.
                var tokens = CssValues.SplitTokens(value.Split('/')[0]);
                if (tokens.Count is >= 1 and <= 4)
                {
                    var radii = new float[4];
                    var percent = new float[4];
                    bool ok = true;
                    for (int i = 0; i < 4; i++) ok &= TryRadius(BoxToken(tokens, i), fs, out radii[i], out percent[i]);
                    if (ok)
                    {
                        Array.Copy(radii, s.Radii, 4);
                        Array.Copy(percent, s.RadiiPercent, 4);
                    }
                }
                break;
            }
            case "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius":
            {
                int corner = d.Name switch { "border-top-left-radius" => 0, "border-top-right-radius" => 1, "border-bottom-right-radius" => 2, _ => 3 };
                if (TryRadius(value.Split(' ')[0], fs, out var cornerPx, out var cornerPct)) { s.Radii[corner] = cornerPx; s.RadiiPercent[corner] = cornerPct; }
                break;
            }
            case "border-top-color" or "border-right-color" or "border-bottom-color" or "border-left-color":
            {
                int side = d.Name switch { "border-top-color" => 0, "border-right-color" => 1, "border-bottom-color" => 2, _ => 3 };
                if (CssValues.TryColor(value, s.Color, out var sideColor)) s.BorderColor[side] = sideColor;
                break;
            }
            case "border-top-width" or "border-right-width" or "border-bottom-width" or "border-left-width":
            {
                int side = d.Name switch { "border-top-width" => 0, "border-right-width" => 1, "border-bottom-width" => 2, _ => 3 };
                if (TryPx(value, fs, out var sideWidth)) s.BorderWidth[side] = sideWidth;
                break;
            }

            case "box-shadow":
                if (EffectValues.TryParseShadows(value, fs, ctx, s.Color, isText: false, out var boxShadows)) s.BoxShadows = boxShadows;
                break;
            case "text-shadow":
                if (EffectValues.TryParseShadows(value, fs, ctx, s.Color, isText: true, out var textShadows)) s.TextShadows = textShadows;
                break;
            case "background-image":
                s.BackgroundGradients = EffectValues.ParseGradients(value, fs, ctx, s.Color);
                break;
            case "transform":
                if (EffectValues.TryParseTransform(value, fs, ctx, out var transform)) s.Transform = transform;
                break;
            case "transform-origin":
                if (EffectValues.TryParsePosition(value, fs, ctx, out var originX, out var originY))
                {
                    s.TransformOriginX = originX;
                    s.TransformOriginY = originY;
                }
                break;
            case "filter":
                if (EffectValues.TryParseFilters(value, fs, ctx, s.Color, out var filters)) s.Filters = filters;
                break;
        }

        void SetLength(string text, float fontSize, Action<Length> assign)
        {
            if (CssValues.TryLength(text, fontSize, ctx, out var len)) assign(len);
        }
    }

    static Overflow ParseOverflow(string value, Overflow current) => value.ToLowerInvariant() switch
    {
        "visible" => Overflow.Visible,
        "hidden" or "clip" => Overflow.Hidden,
        "scroll" => Overflow.Scroll,
        "auto" or "overlay" => Overflow.Auto,
        _ => current
    };

    void ApplyOutlineShorthand(ComputedStyle s, string value, float fontSize)
    {
        float width = 3;
        SKColor? color = null;
        foreach (var token in CssValues.SplitTokens(value.ToLowerInvariant()))
        {
            if (token == "none" || token == "hidden") width = 0;
            else if (token is "solid" or "dashed" or "dotted" or "double" or "auto" or "groove" or "ridge" or "inset" or "outset") { }
            else if (token == "thin") width = 1;
            else if (token == "medium") width = 3;
            else if (token == "thick") width = 5;
            else if (TryPx(token, fontSize, out var px)) width = px;
            else if (CssValues.TryColor(token, s.Color, out var c)) color = c;
        }
        s.OutlineWidth = width;
        s.OutlineColor = color;
    }

    static List<string> CommaList(string value) =>
        EffectValues.SplitTopLevel(value, ',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();

    // Times are written like 200ms, 0.3s, or 0.
    static bool TryTime(string text, out float ms)
    {
        ms = 0;
        text = text.Trim().ToLowerInvariant();
        if (!CssValues.TrySplitUnit(text, out var number, out var unit)) return false;
        switch (unit)
        {
            case "ms": ms = number; return true;
            case "s": ms = number * 1000f; return true;
            case "" when number == 0: return true;
            default: return false;
        }
    }

    static void SetTimes(string value, Action<List<float>> assign)
    {
        var times = new List<float>();
        foreach (var item in CommaList(value))
        {
            if (!TryTime(item, out var ms)) return;
            times.Add(ms);
        }
        if (times.Count > 0) assign(times);
    }

    static void SetTimings(string value, Action<List<TimingFunction>> assign)
    {
        var list = new List<TimingFunction>();
        foreach (var item in CommaList(value))
        {
            if (!TimingFunction.TryParse(item, out var function)) return;
            list.Add(function);
        }
        if (list.Count > 0) assign(list);
    }

    static AnimationDirection ParseAnimationDirection(string v) => v.ToLowerInvariant() switch
    {
        "reverse" => AnimationDirection.Reverse,
        "alternate" => AnimationDirection.Alternate,
        "alternate-reverse" => AnimationDirection.AlternateReverse,
        _ => AnimationDirection.Normal
    };

    static AnimationFill ParseAnimationFill(string v) => v.ToLowerInvariant() switch
    {
        "forwards" => AnimationFill.Forwards,
        "backwards" => AnimationFill.Backwards,
        "both" => AnimationFill.Both,
        _ => AnimationFill.None
    };

    // transition: <property> <duration> <timing-function> <delay>, comma separated. The first time is the duration.
    static void ApplyTransitionShorthand(ComputedStyle s, string value)
    {
        var properties = new List<string>();
        var durations = new List<float>();
        var delays = new List<float>();
        var timings = new List<TimingFunction>();

        foreach (var layer in CommaList(value))
        {
            string property = "all";
            float? duration = null, delay = null;
            var timing = TimingFunction.Ease;

            foreach (var token in CssValues.SplitTokens(layer))
            {
                if (TryTime(token, out var ms)) { if (duration is null) duration = ms; else delay = ms; }
                else if (TimingFunction.TryParse(token, out var fn)) timing = fn;
                else property = token.ToLowerInvariant();
            }

            properties.Add(property);
            durations.Add(duration ?? 0);
            delays.Add(delay ?? 0);
            timings.Add(timing);
        }

        if (properties.Count == 0) return;
        s.TransitionProperty = properties;
        s.TransitionDuration = durations;
        s.TransitionDelay = delays;
        s.TransitionTiming = timings;
    }

    // animation: <name> <duration> <timing> <delay> <iterations> <direction> <fill-mode> <play-state>, comma separated.
    static void ApplyAnimationShorthand(ComputedStyle s, string value)
    {
        var names = new List<string>();
        var durations = new List<float>();
        var delays = new List<float>();
        var timings = new List<TimingFunction>();
        var iterations = new List<float>();
        var directions = new List<AnimationDirection>();
        var fills = new List<AnimationFill>();
        var paused = new List<bool>();

        foreach (var layer in CommaList(value))
        {
            string name = "none";
            float? duration = null, delay = null;
            var timing = TimingFunction.Ease;
            float count = 1;
            var direction = AnimationDirection.Normal;
            var fill = AnimationFill.None;
            bool isPaused = false;

            foreach (var token in CssValues.SplitTokens(layer))
            {
                string t = token.ToLowerInvariant();
                if (TryTime(t, out var ms)) { if (duration is null) duration = ms; else delay = ms; }
                else if (TimingFunction.TryParse(t, out var fn)) timing = fn;
                else if (t == "infinite") count = float.PositiveInfinity;
                else if (t is "normal" or "reverse" or "alternate" or "alternate-reverse") direction = ParseAnimationDirection(t);
                else if (t is "forwards" or "backwards" or "both") fill = ParseAnimationFill(t);
                else if (t is "running" or "paused") isPaused = t == "paused";
                else if (TryNumber(t, out var n)) count = n;
                else name = token;
            }

            names.Add(name);
            durations.Add(duration ?? 0);
            delays.Add(delay ?? 0);
            timings.Add(timing);
            iterations.Add(count);
            directions.Add(direction);
            fills.Add(fill);
            paused.Add(isPaused);
        }

        if (names.Count == 0) return;
        s.AnimationName = names;
        s.AnimationDuration = durations;
        s.AnimationDelay = delays;
        s.AnimationTiming = timings;
        s.AnimationIterations = iterations;
        s.AnimationDirections = directions;
        s.AnimationFills = fills;
        s.AnimationPaused = paused;
    }

    // A radius is a length or a percentage of the box.
    bool TryRadius(string text, float fontSize, out float px, out float percent)
    {
        px = percent = 0;
        if (!CssValues.TryLength(text, fontSize, Context, out var len) || len.IsAuto) return false;
        if (len.Unit == LengthUnit.Percent) percent = len.Value; else px = len.Value;
        return true;
    }

    static bool TryNumber(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    bool TryPx(string text, float fontSize, out float px)
    {
        px = 0;
        if (!CssValues.TryLength(text, fontSize, Context, out var len) || len.Unit != LengthUnit.Px) return false;
        px = len.Value;
        return true;
    }

    static FlexDirection ParseDirection(string value) => value.ToLowerInvariant() switch
    {
        "row-reverse" => FlexDirection.RowReverse,
        "column" => FlexDirection.Column,
        "column-reverse" => FlexDirection.ColumnReverse,
        _ => FlexDirection.Row
    };

    static Align ParseAlign(string value, Align current)
    {
        // "safe"/"unsafe" and "first" are dropped; the rest maps onto one shared keyword set.
        string v = value.ToLowerInvariant().Replace("unsafe ", "").Replace("safe ", "").Replace("first ", "").Trim();
        return v switch
        {
            "auto" => Align.Auto,
            "normal" or "stretch" => Align.Stretch,
            "flex-start" or "start" or "self-start" or "left" => Align.Start,
            "flex-end" or "end" or "self-end" or "right" => Align.End,
            "center" => Align.Center,
            "baseline" => Align.Baseline,
            "space-between" => Align.SpaceBetween,
            "space-around" => Align.SpaceAround,
            "space-evenly" => Align.SpaceEvenly,
            _ => current
        };
    }

    /// <summary>Splits a `place-*` shorthand into its align and justify halves, keeping a "safe"/"unsafe" prefix with its keyword.</summary>
    static (string align, string justify) SplitPlaceValue(string value)
    {
        var raw = CssValues.SplitTokens(value.ToLowerInvariant());
        var parts = new List<string>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i] is "safe" or "unsafe" && i + 1 < raw.Count) parts.Add(raw[i] + " " + raw[++i]);
            else parts.Add(raw[i]);
        }
        if (parts.Count == 0) return ("", "");
        return parts.Count >= 2 ? (parts[0], parts[1]) : (parts[0], parts[0]);
    }

    void ApplyFlexShorthand(ComputedStyle s, string value, float fontSize)
    {
        var tokens = CssValues.SplitTokens(value.ToLowerInvariant());
        if (tokens.Count == 1)
        {
            switch (tokens[0])
            {
                case "none": s.FlexGrow = 0; s.FlexShrink = 0; s.FlexBasis = Length.Auto; return;
                case "auto": s.FlexGrow = 1; s.FlexShrink = 1; s.FlexBasis = Length.Auto; return;
                case "initial": s.FlexGrow = 0; s.FlexShrink = 1; s.FlexBasis = Length.Auto; return;
            }
        }

        float grow = 1, shrink = 1;
        var basis = new Length(0, LengthUnit.Percent);
        int numbers = 0;
        foreach (var token in tokens)
        {
            if (TryNumber(token, out var n))
            {
                if (numbers == 0) grow = n; else if (numbers == 1) shrink = n; else return;
                numbers++;
            }
            else if (CssValues.TryLength(token, fontSize, Context, out var len)) basis = len;
            else return;
        }
        s.FlexGrow = Math.Max(0, grow);
        s.FlexShrink = Math.Max(0, shrink);
        s.FlexBasis = basis;
    }

    bool ExpandBox(string value, float fontSize, Length[] target)
    {
        var tokens = CssValues.SplitTokens(value);
        if (tokens.Count is 0 or > 4) return false;

        var parsed = new Length[4];
        for (int i = 0; i < 4; i++)
            if (!CssValues.TryLength(BoxToken(tokens, i), fontSize, Context, out parsed[i])) return false;

        Array.Copy(parsed, target, 4);
        return true;
    }

    void SetBox(string value, float fontSize, Length[] target) => ExpandBox(value, fontSize, target);

    // CSS box shorthand: 1 value = all, 2 = vertical/horizontal, 3 = top/horizontal/bottom, 4 = clockwise.
    static string BoxToken(List<string> tokens, int side) => tokens.Count switch
    {
        1 => tokens[0],
        2 => tokens[side % 2],
        3 => side == 3 ? tokens[1] : tokens[side],
        _ => tokens[side]
    };

    void ApplyBorderShorthand(ComputedStyle s, string value, bool all, int side, float fontSize)
    {
        float width = 3; // "medium"
        SKColor? color = null;
        bool none = false;

        foreach (var token in CssValues.SplitTokens(value))
        {
            string t = token.ToLowerInvariant();
            if (t is "none" or "hidden") none = true;
            else if (t is "solid" or "dashed" or "dotted" or "double" or "groove" or "ridge" or "inset" or "outset") { }
            else if (t is "thin") width = 1;
            else if (t is "medium") width = 3;
            else if (t is "thick") width = 5;
            else if (CssValues.TryLength(t, fontSize, Context, out var len) && len.Unit == LengthUnit.Px) width = len.Value;
            else if (CssValues.TryColor(t, s.Color, out var c)) color = c;
        }

        if (none) width = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!all && i != side) continue;
            s.BorderWidth[i] = width;
            s.BorderColor[i] = color;
        }
    }

    static void Inherit(ComputedStyle s, ComputedStyle parent, string property)
    {
        switch (property)
        {
            case "color": s.Color = parent.Color; break;
            case "font-size": s.FontSize = parent.FontSize; break;
            case "font-family": s.FontFamily = parent.FontFamily; break;
            case "font-weight": s.FontWeight = parent.FontWeight; break;
            case "font-style": s.Italic = parent.Italic; break;
            case "line-height": s.LineHeight = parent.LineHeight; s.LineHeightIsMultiplier = parent.LineHeightIsMultiplier; break;
            case "text-align": s.TextAlign = parent.TextAlign; break;
            case "background-color": s.BackgroundColor = parent.BackgroundColor; break;
            case "opacity": s.Opacity = parent.Opacity; break;
            case "display": s.Display = parent.Display; break;
        }
    }
}

static class UserAgentStyles
{
    public const string Css = """
        head, style, script, title, meta, link { display: none; }
        body { display: block; margin: 0; color: #000; font-family: "Segoe UI", sans-serif; font-size: 16px; }
        div, p, h1, h2, h3, h4, h5, h6, ul, ol, li, section, article, header, footer, nav, main, aside, form, pre, hr { display: block; }
        hr { height: 0; margin: 0.5em 0; border: none; border-top: 1px solid #8f8f8f; }
        span, a, b, i, u, strong, em, small, label, code { display: inline; }
        h1 { font-size: 2em; margin: 0.67em 0; font-weight: 700; }
        h2 { font-size: 1.5em; margin: 0.83em 0; font-weight: 700; }
        h3 { font-size: 1.17em; margin: 1em 0; font-weight: 700; }
        h4 { font-size: 1em; margin: 1.33em 0; font-weight: 700; }
        h5 { font-size: 0.83em; margin: 1.67em 0; font-weight: 700; }
        h6 { font-size: 0.67em; margin: 2.33em 0; font-weight: 700; }
        p { margin: 1em 0; }
        b, strong { font-weight: 700; }
        i, em { font-style: italic; }
        a { color: #0000ee; }
        button {
            display: inline-block; padding: 1px 6px; font-size: 13.333px; text-align: center;
            color: #000; background-color: #efefef; border: 1px solid #767676; border-radius: 3px;
        }
        button:disabled { color: #8a8a8a; border-color: #c4c4c4; }
        a { cursor: pointer; text-decoration: underline; }
        a:focus-visible, button:focus-visible, [tabindex]:focus-visible { outline: 2px solid #3b82f6; outline-offset: 2px; }
        label { cursor: default; }
        input, textarea, select {
            display: inline-block; font-size: 13.333px; color: #000; background-color: #fff; cursor: text;
            border: 1px solid #767676; border-radius: 2px; padding: 2px 4px;
        }
        textarea { padding: 4px; }
        select { cursor: default; box-sizing: border-box; padding: 2px 24px 2px 6px; }
        input[type=checkbox], input[type=radio] {
            width: 13px; height: 13px; margin: 3px 3px 3px 4px; padding: 0; border: none; background-color: transparent; cursor: default;
        }
        input[type=button], input[type=submit], input[type=reset] {
            padding: 1px 6px; text-align: center; background-color: #efefef; cursor: default; border-radius: 3px;
        }
        input[type=hidden], option, optgroup, datalist { display: none; }
        input:disabled, textarea:disabled, select:disabled { color: #8a8a8a; background-color: #f0f0f0; border-color: #c4c4c4; }
        input[type=checkbox]:disabled, input[type=radio]:disabled { background-color: transparent; }
        input:focus-visible, textarea:focus-visible, select:focus-visible { outline: 2px solid #3b82f6; outline-offset: 0; }
        input[type=checkbox]:focus-visible, input[type=radio]:focus-visible { outline-offset: 2px; }
        """;
}

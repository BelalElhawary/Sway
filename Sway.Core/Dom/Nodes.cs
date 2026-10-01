using Sway.Core.Layout;
using Sway.Core.Styling;
using SkiaSharp;

namespace Sway.Core.Dom;

/// <summary>
/// Owns the node tree plus the dirty flags that drive restyle and relayout.
/// All tree mutation, style, layout and paint happen under <see cref="SyncRoot"/>.
/// </summary>
public sealed class Document
{
    public readonly object SyncRoot = new();
    public ElementNode Root { get; }
    public int StructureVersion { get; private set; }
    /// <summary>Something needs restyling; see <see cref="FullStyleDirty"/> and <see cref="StyleRoots"/> for how much.</summary>
    public bool StyleDirty { get; set; } = true;
    public bool LayoutDirty { get; set; } = true;

    /// <summary>When set, every element is restyled. Otherwise only the subtrees under <see cref="StyleRoots"/>.</summary>
    public bool FullStyleDirty { get; set; } = true;
    public HashSet<ElementNode> StyleRoots { get; } = new();

    /// <summary>Supplied by the style engine so the document can ask which state changes can affect styles.</summary>
    public IStyleInvalidation? StyleInvalidation { get; set; }

    /// <summary>Bumped when every cached layout result must be discarded (new stylesheet, new fonts).</summary>
    public int LayoutEpoch { get; private set; }

    /// <summary>Bumped whenever something that affects pixels changes; caches of painted output compare against it.</summary>
    public int PaintVersion { get; private set; }

    public void InvalidatePaint() => PaintVersion++;

    /// <summary>Restyle one element and its subtree (and its siblings' subtrees when sibling selectors exist).</summary>
    public void InvalidateStyleOf(ElementNode element)
    {
        StyleDirty = true;
        if (FullStyleDirty) return;

        var target = StyleInvalidation is { HasSiblingSelectors: true } ? element.ParentElement ?? element : element;
        StyleRoots.Add(target);
    }

    /// <summary>A pseudo-class state (hover, focus, ...) flipped; restyle only if some rule can depend on it.</summary>
    public void InvalidateStyleForState(ElementNode element, ElementState flag)
    {
        if (StyleInvalidation is null || StyleInvalidation.StateMayAffect(element, flag)) InvalidateStyleOf(element);
    }

    /// <summary>Size of the window in logical pixels; the root element scrolls inside it.</summary>
    public float ViewportWidth { get; set; }
    public float ViewportHeight { get; set; }

    public Document() => Root = new ElementNode(this, "body");

    /// <summary>The children of a container changed: restyle and relayout the element that owns them.</summary>
    public void InvalidateStructureAt(ContainerNode container)
    {
        StructureVersion++;
        LayoutDirty = true;

        var owner = container as ElementNode ?? container.ParentElement;
        if (owner is null) return;
        owner.MarkLayoutDirty();
        InvalidateStyleOf(owner);
    }

    /// <summary>Restyle everything, for example after a stylesheet or the viewport changed.</summary>
    public void InvalidateStyle()
    {
        StyleDirty = true;
        FullStyleDirty = true;
    }

    /// <summary>Discard every cached layout result and lay everything out again.</summary>
    public void InvalidateAllLayout()
    {
        LayoutEpoch++;
        LayoutDirty = true;
    }
}

/// <summary>What the document needs to know about the style engine to invalidate narrowly.</summary>
public interface IStyleInvalidation
{
    bool HasSiblingSelectors { get; }

    /// <summary>Could any rule give <paramref name="element"/> (or its descendants) a different style when the state flips?</summary>
    bool StateMayAffect(ElementNode element, ElementState flag);
}

public abstract class Node
{
    protected Node(Document document) => Document = document;

    public Document Document { get; }
    public ContainerNode? Parent { get; internal set; }

    /// <summary>Index inside the parent element's <see cref="ElementNode.PhysicalChildren"/>.</summary>
    internal int PhysIndex;

    /// <summary>Nearest ancestor that is a real element (skips component and markup containers).</summary>
    public ElementNode? ParentElement
    {
        get
        {
            Node? p = Parent;
            while (p is not null and not ElementNode) p = p.Parent;
            return p as ElementNode;
        }
    }
}

/// <summary>Holds children without being drawn: component boundaries and markup blocks.</summary>
public abstract class ContainerNode : Node
{
    protected ContainerNode(Document document) : base(document) { }

    public List<Node> Children { get; } = new();

    public void Insert(int index, Node child)
    {
        child.Parent = this;
        Children.Insert(index, child);
        Document.InvalidateStructureAt(this);
    }

    public void Append(Node child) => Insert(Children.Count, child);

    public void RemoveAt(int index)
    {
        Children[index].Parent = null;
        Children.RemoveAt(index);
        Document.InvalidateStructureAt(this);
    }

    public void Reorder(Node[] newOrder)
    {
        Children.Clear();
        Children.AddRange(newOrder);
        Document.InvalidateStructureAt(this);
    }
}

public sealed class ComponentNode : ContainerNode
{
    public ComponentNode(Document document) : base(document) { }
}

public sealed class TextNode : Node
{
    string _text;

    public TextNode(Document document, string text) : base(document) => _text = text;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            Document.LayoutDirty = true;
            ParentElement?.MarkLayoutDirty();
        }
    }

    /// <summary>Filled by layout.</summary>
    public List<TextRun> Runs { get; } = new();
}

public readonly record struct TextRun(string Text, float X, float Baseline, float Width);

[Flags]
public enum ElementState { None = 0, Hover = 1, Active = 2, Focus = 4, FocusVisible = 8 }

public sealed class ElementNode : ContainerNode
{
    static readonly string[] NoClasses = Array.Empty<string>();

    readonly List<Node> _physical = new();
    int _physicalVersion = -1;
    string[] _classes = NoClasses;

    public ElementNode(Document document, string tag) : base(document) => Tag = tag.ToLowerInvariant();

    public string Tag { get; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Event name (without "on") to Blazor event handler id.</summary>
    public Dictionary<string, ulong> Handlers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ElementState State { get; private set; }
    public ComputedStyle Style { get; set; } = ComputedStyle.CreateRoot();

    /// <summary>False until the first style resolve; transitions never run for a brand-new element.</summary>
    public bool HasComputedStyle { get; set; }

    /// <summary>Running transitions and animations, if any.</summary>
    public AnimationState? Animation { get; set; }

    // Layout output, in window coordinates.
    public SKRect BorderRect { get; set; }
    public SKRect ContentRect { get; set; }

    /// <summary>Offset applied by position: relative, remembered so inline placement can undo it.</summary>
    public SKPoint RelativeOffset { get; set; }

    // ---- layout cache: a clean subtree laid out with the same inputs only needs moving ----

    /// <summary>True when this element or anything below it changed since it was last laid out.</summary>
    public bool SubtreeLayoutDirty { get; set; } = true;

    /// <summary>True once an out-of-flow descendant exists; such subtrees are never reused from cache.</summary>
    public bool ContainsOutOfFlow { get; set; }

    internal LayoutKey? CacheKey;
    internal float CacheX, CacheY, CacheMarginHeight;
    internal int CacheEpoch = -1;

    /// <summary>True when visual bounds and scroll limits must be recomputed for this subtree.</summary>
    public bool ExtentsDirty { get; set; } = true;

    /// <summary>Marks this element and every ancestor as needing layout.</summary>
    public void MarkLayoutDirty()
    {
        Document.LayoutDirty = true;
        for (var e = this; e is not null; e = e.ParentElement)
        {
            e.SubtreeLayoutDirty = true;
            e.ExtentsDirty = true;
        }
    }

    /// <summary>Bounds of everything this element paints (itself and unclipped descendants), set by layout.</summary>
    public SKRect VisualBounds { get; set; }
    public bool HasVisualBounds { get; set; }

    /// <summary>
    /// True once any descendant has been styled as a positioned or composited box. Sticky on purpose: it only
    /// lets the display list skip subtrees that certainly contain none.
    /// </summary>
    public bool HasEntryDescendant { get; set; }

    // Scrolling. Offsets persist across re-renders because the node does.
    public float ScrollX { get; set; }
    public float ScrollY { get; set; }
    public float MaxScrollX { get; set; }
    public float MaxScrollY { get; set; }

    /// <summary>Area inside the borders. The root element's padding box is the viewport.</summary>
    public SKRect PaddingBox
    {
        get
        {
            if (Parent is null) return new SKRect(0, 0, Document.ViewportWidth, Document.ViewportHeight);
            var w = Style.BorderWidth;
            return new SKRect(BorderRect.Left + w[ComputedStyle.Left], BorderRect.Top + w[ComputedStyle.Top],
                BorderRect.Right - w[ComputedStyle.Right], BorderRect.Bottom - w[ComputedStyle.Bottom]);
        }
    }

    /// <summary>The root always scrolls; other elements scroll when overflow is auto or scroll.</summary>
    public bool ScrollsX => Parent is null || Style.OverflowX is Overflow.Auto or Overflow.Scroll;
    public bool ScrollsY => Parent is null || Style.OverflowY is Overflow.Auto or Overflow.Scroll;

    /// <summary>Whether content is clipped to the padding box (and so may be scrolled programmatically).</summary>
    public bool ClipsContent => Parent is null || Style.ClipsContent;

    public bool IsFocusable
    {
        get
        {
            if (IsDisabled) return false;
            if (Attributes.ContainsKey("tabindex")) return true;
            return Tag switch
            {
                "button" or "input" or "textarea" or "select" => true,
                "a" => Attributes.ContainsKey("href"),
                _ => false
            };
        }
    }

    /// <summary>Position in sequential focus navigation; negative means focusable but skipped by Tab.</summary>
    public int TabIndex => Attributes.TryGetValue("tabindex", out var v) && int.TryParse(v, out var n) ? n : 0;

    public string? Id => Attributes.TryGetValue("id", out var id) ? id : null;
    public IReadOnlyList<string> Classes => _classes;
    public bool IsDisabled => Attributes.ContainsKey("disabled");

    public string? GetAttribute(string name) => Attributes.TryGetValue(name, out var v) ? v : null;

    /// <summary>Editing state for text controls, created on first use. The value attribute stays the model's view of it.</summary>
    public TextEditState? Edit { get; set; }

    public void SetAttribute(string name, string value)
    {
        Attributes[name] = value;
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
            _classes = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        else if (Edit is { } edit)
        {
            if (name.Equals("value", StringComparison.OrdinalIgnoreCase)) edit.SetValueExternal(value);
            else if (name.Equals("maxlength", StringComparison.OrdinalIgnoreCase)) edit.MaxLength = int.TryParse(value, out var n) ? n : -1;
        }

        // Controls size themselves from attributes such as size, rows and value, which style comparison cannot see.
        if (Controls.IsReplaced(this) || Tag is "option" or "optgroup") MarkLayoutDirty();
        Document.InvalidateStyleOf(this);
    }

    public void RemoveAttribute(string name)
    {
        if (!Attributes.Remove(name)) return;
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase)) _classes = NoClasses;
        else if (Edit is { } edit)
        {
            if (name.Equals("value", StringComparison.OrdinalIgnoreCase)) edit.SetValueExternal("");
            else if (name.Equals("maxlength", StringComparison.OrdinalIgnoreCase)) edit.MaxLength = -1;
        }

        if (Controls.IsReplaced(this) || Tag is "option" or "optgroup") MarkLayoutDirty();
        Document.InvalidateStyleOf(this);
    }

    public void SetState(ElementState flag, bool on)
    {
        var next = on ? State | flag : State & ~flag;
        if (next == State) return;
        State = next;
        Document.InvalidateStyleForState(this, flag);
    }

    /// <summary>Drawable children (elements and text) with component containers flattened away.</summary>
    public IReadOnlyList<Node> PhysicalChildren
    {
        get
        {
            if (_physicalVersion != Document.StructureVersion)
            {
                _physical.Clear();
                Flatten(this, _physical);
                for (int i = 0; i < _physical.Count; i++) _physical[i].PhysIndex = i;
                _physicalVersion = Document.StructureVersion;
            }
            return _physical;
        }
    }

    static void Flatten(ContainerNode container, List<Node> into)
    {
        foreach (var child in container.Children)
        {
            if (child is ElementNode or TextNode) into.Add(child);
            else if (child is ContainerNode nested) Flatten(nested, into);
        }
    }

    public ElementNode? PreviousSiblingElement()
    {
        var siblings = ParentElement?.PhysicalChildren;
        if (siblings is null) return null;
        for (int i = PhysIndex - 1; i >= 0; i--)
            if (siblings[i] is ElementNode e) return e;
        return null;
    }

    /// <summary>1-based position among element siblings, and the sibling element count.</summary>
    public (int index, int count) ElementPosition()
    {
        var siblings = ParentElement?.PhysicalChildren;
        if (siblings is null) return (1, 1);
        int index = 0, count = 0;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] is not ElementNode) continue;
            count++;
            if (i == PhysIndex) index = count;
        }
        return (index, count);
    }
}

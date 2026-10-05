using SkiaSharp;

namespace Sway.Widgets;

sealed class LazyViewportElement : RenderObjectElement
{
    readonly Dictionary<int, Element> _children = new();
    int _version;
    int _builtVersion = -1;
    int _lastFirst = int.MinValue, _lastLast = int.MinValue;

    public LazyViewportElement(LazyViewport widget) : base(widget) { }

    LazyViewport W => (LazyViewport)Widget;
    RenderLazyViewport RO => (RenderLazyViewport)RenderObject;

    public override void Mount(Element? parent)
    {
        base.Mount(parent);
        Configure();
        RO.BuildRange = BuildRange;
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _version++;
        Configure();
    }

    void Configure() => RO.Configure(W.Axis, W.Position, W.ItemCount, W.ItemExtent, W.Padding);

    void BuildRange(int first, int last)
    {
        bool rebuild = _builtVersion != _version;
        _builtVersion = _version;

        // Scrolling within the same window of items needs no unmounting, rebuilding or child-list resync.
        if (!rebuild && first == _lastFirst && last == _lastLast && _children.Count == Math.Max(0, Math.Min(last, W.ItemCount - 1) - first + 1)) return;
        _lastFirst = first; _lastLast = last;

        foreach (var idx in _children.Keys.Where(i => i < first || i > last || i >= W.ItemCount).ToList())
        {
            _children[idx].Unmount();
            _children.Remove(idx);
        }

        for (int i = first; i <= last && i < W.ItemCount; i++)
        {
            bool have = _children.TryGetValue(i, out var existing);
            if (have && !rebuild) continue;
            var widget = new KeyedSubtree(new ValueKey<int>(i), new Builder(ctx => W.Builder(ctx, i)));
            var child = UpdateChildPublic(existing, widget);
            if (child is not null) _children[i] = child;
        }
        ResyncChildren();
    }

    Element? UpdateChildPublic(Element? existing, Widget widget) => UpdateChild(existing, widget);

    internal override void ResyncChildren()
    {
        var boxes = new List<RenderBox>();
        foreach (var (index, element) in _children.OrderBy(kv => kv.Key))
        {
            if (element.FindRenderObject() is not RenderBox box) continue;
            if (box.ParentData is not LazyListParentData pd) box.ParentData = pd = new LazyListParentData();
            pd.Index = index;
            boxes.Add(box);
        }
        RO.SetChildren(boxes);
    }

    public override void VisitChildren(Action<Element> visitor)
    {
        foreach (var c in _children.Values) visitor(c);
    }
}

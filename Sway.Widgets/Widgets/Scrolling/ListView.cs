using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A scrolling list. <c>new ListView(children)</c> or <c>ListView.Builder(count, builder, itemExtent)</c> for lazy lists.</summary>
public sealed class ListView : StatelessWidget
{
    readonly IReadOnlyList<Widget>? _children;
    readonly int _itemCount;
    readonly Func<BuildContext, int, Widget>? _builder;
    readonly float? _itemExtent;
    readonly Axis _axis;
    readonly ScrollController? _controller;
    readonly EdgeInsets? _padding;

    public ListView(IReadOnlyList<Widget> children, Axis scrollDirection = Axis.Vertical, ScrollController? controller = null,
        EdgeInsets? padding = null, Key? key = null) : base(key)
    {
        _children = children; _axis = scrollDirection; _controller = controller; _padding = padding;
    }

    ListView(int itemCount, Func<BuildContext, int, Widget> builder, float? itemExtent, Axis axis, ScrollController? controller,
        EdgeInsets? padding, Key? key) : base(key)
    {
        _itemCount = itemCount; _builder = builder; _itemExtent = itemExtent; _axis = axis; _controller = controller; _padding = padding;
    }

    /// <summary>
    /// Builds items on demand. Pass <paramref name="itemExtent"/> when every item has the same main-axis size (fastest and exact); otherwise
    /// items are measured as they scroll into view and unmeasured ones are estimated from the average.
    /// </summary>
    public static ListView Builder(int itemCount, Func<BuildContext, int, Widget> itemBuilder, float? itemExtent = null,
        Axis scrollDirection = Axis.Vertical, ScrollController? controller = null, EdgeInsets? padding = null, Key? key = null) =>
        new(itemCount, itemBuilder, itemExtent, scrollDirection, controller, padding, key);

    public override Widget Build(BuildContext context)
    {
        if (_children is not null)
            return new SingleChildScrollView(
                new Flex(_axis, _children, MainAxisAlignment.Start, MainAxisSize.Min, CrossAxisAlignment.Stretch),
                _axis, _controller, _padding);

        return new Scrollable(_axis, (_, position) =>
            new LazyViewport(_axis, position, _itemCount, _builder!, _itemExtent, _padding ?? EdgeInsets.Zero), _controller);
    }
}

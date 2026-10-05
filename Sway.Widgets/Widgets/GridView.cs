namespace Sway.Widgets;

/// <summary>
/// A scrolling grid that builds only the visible rows. Give either <c>crossAxisCount</c> columns or a
/// <c>minColumnWidth</c> (as many columns as fit, sharing the leftover width), and either a fixed
/// <c>mainAxisExtent</c> (cell height) or a <c>childAspectRatio</c> (width / height, default 1).
/// </summary>
public sealed class GridView : StatelessWidget
{
    readonly int _itemCount;
    readonly Func<BuildContext, int, Widget> _itemBuilder;
    readonly int? _crossAxisCount;
    readonly float? _minColumnWidth, _mainAxisExtent;
    readonly float _childAspectRatio, _crossAxisSpacing, _mainAxisSpacing;
    readonly ScrollController? _controller;
    readonly EdgeInsets _padding;

    GridView(int itemCount, Func<BuildContext, int, Widget> itemBuilder, int? crossAxisCount, float? minColumnWidth, float? mainAxisExtent,
        float childAspectRatio, float crossAxisSpacing, float mainAxisSpacing, ScrollController? controller, EdgeInsets padding, Key? key) : base(key)
    {
        if (crossAxisCount is null == minColumnWidth is null)
            throw new ArgumentException("Give exactly one of crossAxisCount and minColumnWidth.");
        if (crossAxisCount is < 1) throw new ArgumentOutOfRangeException(nameof(crossAxisCount));
        if (childAspectRatio <= 0) throw new ArgumentOutOfRangeException(nameof(childAspectRatio));
        _itemCount = itemCount; _itemBuilder = itemBuilder; _crossAxisCount = crossAxisCount; _minColumnWidth = minColumnWidth;
        _mainAxisExtent = mainAxisExtent; _childAspectRatio = childAspectRatio; _crossAxisSpacing = crossAxisSpacing;
        _mainAxisSpacing = mainAxisSpacing; _controller = controller; _padding = padding;
    }

    public static GridView Builder(int itemCount, Func<BuildContext, int, Widget> itemBuilder, int? crossAxisCount = null,
        float? minColumnWidth = null, float? mainAxisExtent = null, float childAspectRatio = 1, float crossAxisSpacing = 0,
        float mainAxisSpacing = 0, ScrollController? controller = null, EdgeInsets? padding = null, Key? key = null) =>
        new(itemCount, itemBuilder, crossAxisCount, minColumnWidth, mainAxisExtent, childAspectRatio, crossAxisSpacing,
            mainAxisSpacing, controller, padding ?? EdgeInsets.Zero, key);

    public override Widget Build(BuildContext context) => new LayoutBuilder((_, constraints) =>
    {
        float width = constraints.HasBoundedWidth ? constraints.MaxWidth : 400;
        float inner = Math.Max(0, width - _padding.Horizontal);
        int columns = _crossAxisCount ?? Math.Max(1, (int)MathF.Floor((inner + _crossAxisSpacing) / (_minColumnWidth!.Value + _crossAxisSpacing)));
        float cellWidth = Math.Max(0, (inner - _crossAxisSpacing * (columns - 1)) / columns);
        float cellHeight = _mainAxisExtent ?? cellWidth / _childAspectRatio;
        float rowExtent = cellHeight + _mainAxisSpacing;
        int rows = (_itemCount + columns - 1) / columns;

        // Each lazily built list item is one row of cells; the list's fixed extent makes row layout exact and cheap.
        return ListView.Builder(rows, (ctx, row) =>
        {
            var cells = new List<Widget>(columns);
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                cells.Add(new Expanded(index < _itemCount ? _itemBuilder(ctx, index) : new SizedBox()));
            }
            return new Padding(EdgeInsets.Only(bottom: _mainAxisSpacing),
                new Row(cells, crossAxisAlignment: CrossAxisAlignment.Stretch, spacing: _crossAxisSpacing));
        }, itemExtent: rowExtent, controller: _controller, padding: _padding);
    });
}

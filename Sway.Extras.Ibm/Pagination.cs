using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>
/// Carbon's pagination bar: items per page, the range being shown, and previous/next buttons. Pages are zero-based.
/// Stateless: the owner keeps <paramref name="page"/> and <paramref name="pageSize"/>.
/// </summary>
public sealed class Pagination(int totalItems, int page, int pageSize, Action<int> onPageChanged, Action<int>? onPageSizeChanged = null,
    IReadOnlyList<int>? pageSizes = null, Key? key = null) : StatelessWidget(key)
{
    public static readonly IReadOnlyList<int> DefaultPageSizes = [10, 20, 30, 40, 50];

    /// <summary>The number of pages needed for <paramref name="totalItems"/> items; at least 1.</summary>
    public static int PageCount(int totalItems, int pageSize) => Math.Max(1, (totalItems + pageSize - 1) / Math.Max(1, pageSize));

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var secondary = theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnSurfaceVariant));
        int pages = PageCount(totalItems, pageSize);
        int current = Math.Clamp(page, 0, pages - 1);
        int first = totalItems == 0 ? 0 : current * pageSize + 1;
        int last = Math.Min(totalItems, (current + 1) * pageSize);
        var sizes = (pageSizes ?? DefaultPageSizes).Contains(pageSize) ? pageSizes ?? DefaultPageSizes : [.. pageSizes ?? DefaultPageSizes, pageSize];

        Widget Divider() => new Container(width: 1, height: 24, color: s.OutlineVariant);

        return new Container(height: Math.Max(48, theme.Shape.FieldHeight), color: s.SurfaceContainerHigh, child: new Column(
            crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Container(height: 1, color: s.OutlineVariant),
            new Expanded(new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 12, children:
            [
                new SizedBox(width: 4),
                ..onPageSizeChanged is null ? Array.Empty<Widget>() :
                [
                    new Text("Items per page:", style: secondary),
                    new SizedBox(width: 88, child: new DropdownButton<int>(
                        sizes.Select(n => new DropdownMenuItem<int>(n, new Text(n.ToString()))).ToList(), pageSize, onPageSizeChanged)),
                    Divider(),
                ],
                new Text($"{first}–{last} of {totalItems} item{(totalItems == 1 ? "" : "s")}", style: secondary),
                new Expanded(new SizedBox()),
                new Text($"{current + 1} of {pages} page{(pages == 1 ? "" : "s")}", style: secondary),
                new IconButton(new Icon(Icons.ChevronLeft), current > 0 ? () => onPageChanged(current - 1) : null),
                new IconButton(new Icon(Icons.ChevronRight), current < pages - 1 ? () => onPageChanged(current + 1) : null),
            ])),
        ]));
    }
}

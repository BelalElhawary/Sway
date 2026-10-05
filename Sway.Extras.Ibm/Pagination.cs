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
        var theme = CarbonTheme.Of(context);
        var k = theme.Colors;
        var secondary = theme.Type.Label01.Merge(new TextStyle(Color: k.TextSecondary));
        int pages = PageCount(totalItems, pageSize);
        int current = Math.Clamp(page, 0, pages - 1);
        int first = totalItems == 0 ? 0 : current * pageSize + 1;
        int last = Math.Min(totalItems, (current + 1) * pageSize);
        var sizes = (pageSizes ?? DefaultPageSizes).Contains(pageSize) ? pageSizes ?? DefaultPageSizes : [.. pageSizes ?? DefaultPageSizes, pageSize];

        Widget Divider() => new Container(width: 1, height: 24, color: k.BorderSubtle01);

        // Narrow bars give up the wordy parts first: the "Items per page" label, then the page count.
        return new LayoutBuilder((ctx, box) =>
        {
            bool roomy = box.MaxWidth >= 600, medium = box.MaxWidth >= 440;
            return new Container(height: 48, color: k.Layer01, child: new Column(
                crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                new Container(height: 1, color: k.BorderSubtle01),
                new Expanded(new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 12, children:
                [
                    new SizedBox(width: 4),
                    ..onPageSizeChanged is null ? Array.Empty<Widget>() :
                    [
                        ..roomy ? [new Text(CarbonLocalizations.Of(context).ItemsPerPage, style: secondary)] : Array.Empty<Widget>(),
                        new SizedBox(width: 88, child: new CarbonDropdown<int>(
                            sizes.Select(n => new CarbonDropdownItem<int>(n, n.ToString())).ToList(), pageSize, onPageSizeChanged, size: CarbonFieldSize.Large, onLayer: true)),
                        Divider(),
                    ],
                    new Expanded(new Text(CarbonLocalizations.Of(context).ItemRange(first, last, totalItems, roomy),
                        style: secondary, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1)),
                    ..medium ? [new Text(CarbonLocalizations.Of(context).PageOf(current + 1, pages), style: secondary)] : Array.Empty<Widget>(),
                    new CarbonIconButton(Icons.ChevronLeft, current > 0 ? () => onPageChanged(current - 1) : null, CarbonButtonSize.Large),
                    new CarbonIconButton(Icons.ChevronRight, current < pages - 1 ? () => onPageChanged(current + 1) : null, CarbonButtonSize.Large),
                ])),
            ]));
        });
    }
}

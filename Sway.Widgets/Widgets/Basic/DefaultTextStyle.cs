using SkiaSharp;

namespace Sway.Widgets;

public sealed class DefaultTextStyle(TextStyle style, Widget child, TextAlign? textAlign = null, bool softWrap = true,
    TextOverflow overflow = TextOverflow.Clip, int? maxLines = null, Key? key = null) : InheritedWidget(child, key)
{
    public TextStyle Style { get; } = style;
    public TextAlign? TextAlign { get; } = textAlign;
    public bool SoftWrap { get; } = softWrap;
    public TextOverflow Overflow { get; } = overflow;
    public int? MaxLines { get; } = maxLines;

    public override bool UpdateShouldNotify(InheritedWidget old)
    {
        var o = (DefaultTextStyle)old;
        return !Style.Equals(o.Style) || TextAlign != o.TextAlign || SoftWrap != o.SoftWrap || Overflow != o.Overflow || MaxLines != o.MaxLines;
    }

    public static DefaultTextStyle? Of(BuildContext context) => context.DependOn<DefaultTextStyle>();

    /// <summary>Replaces the style for a subtree, merging over the inherited one.</summary>
    public static Widget Merge(BuildContext context, TextStyle style, Widget child)
    {
        var inherited = Of(context);
        return new DefaultTextStyle(inherited is null ? style : inherited.Style.Merge(style), child,
            inherited?.TextAlign, inherited?.SoftWrap ?? true, inherited?.Overflow ?? TextOverflow.Clip, inherited?.MaxLines);
    }
}

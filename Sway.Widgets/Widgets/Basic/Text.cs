using SkiaSharp;

namespace Sway.Widgets;

public sealed class Text : StatelessWidget
{
    readonly string? _data;
    readonly TextSpan? _span;
    readonly TextStyle? _style;
    readonly TextAlign? _textAlign;
    readonly TextDirection? _textDirection;
    readonly bool? _softWrap;
    readonly TextOverflow? _overflow;
    readonly int? _maxLines;

    public Text(string data, TextStyle? style = null, TextAlign? textAlign = null, TextDirection? textDirection = null,
        bool? softWrap = null, TextOverflow? overflow = null, int? maxLines = null, Key? key = null) : base(key)
    {
        _data = data; _style = style; _textAlign = textAlign; _textDirection = textDirection;
        _softWrap = softWrap; _overflow = overflow; _maxLines = maxLines;
    }

    /// <summary>Rich text: <c>Text.Rich(new TextSpan("a ", children: [new TextSpan("b", bold)]))</c>.</summary>
    public Text(TextSpan span, TextStyle? style = null, TextAlign? textAlign = null, TextDirection? textDirection = null,
        bool? softWrap = null, TextOverflow? overflow = null, int? maxLines = null, Key? key = null) : base(key)
    {
        _span = span; _style = style; _textAlign = textAlign; _textDirection = textDirection;
        _softWrap = softWrap; _overflow = overflow; _maxLines = maxLines;
    }

    public override Widget Build(BuildContext context)
    {
        var d = DefaultTextStyle.Of(context);
        var style = TextStyle.Fallback.Merge(d?.Style).Merge(_style);
        return new RichText(
            _span ?? new TextSpan(_data),
            style,
            _textAlign ?? d?.TextAlign ?? TextAlign.Start,
            _textDirection ?? Directionality.Of(context),
            _softWrap ?? d?.SoftWrap ?? true,
            _overflow ?? d?.Overflow ?? TextOverflow.Clip,
            _maxLines ?? d?.MaxLines);
    }
}

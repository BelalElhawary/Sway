using SkiaSharp;

namespace Sway.Widgets;

// ---- inherited configuration ----

public sealed class Directionality(TextDirection textDirection, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public TextDirection TextDirection { get; } = textDirection;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((Directionality)old).TextDirection != TextDirection;

    public static TextDirection Of(BuildContext context) => context.DependOn<Directionality>()?.TextDirection ?? TextDirection.Ltr;
}

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

public sealed class ErrorWidget(string message) : StatelessWidget
{
    public override Widget Build(BuildContext context) =>
        new ColoredBox(new SKColor(0xFF, 0xE0, 0xE0), new Padding(EdgeInsets.All(8),
            new Text(message, style: new TextStyle(Color: Colors.Red, FontSize: 12))));
}

// ---- text ----

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

public sealed class RichText(TextSpan text, TextStyle style, TextAlign textAlign, TextDirection textDirection,
    bool softWrap, TextOverflow overflow, int? maxLines, Key? key = null) : LeafRenderObjectWidget(key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderParagraph(text, style, textAlign, textDirection, softWrap, overflow, maxLines);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderParagraph)ro).Update(text, style, textAlign, textDirection, softWrap, overflow, maxLines);
}

// ---- single-child layout ----

public sealed class Padding(IEdgeInsetsLike padding, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public Padding(EdgeInsets padding, Widget? child = null, Key? key = null) : this(new PhysicalInsets(padding), child, key) { }
    public Padding(EdgeInsetsDirectional padding, Widget? child = null, Key? key = null) : this(new DirectionalInsets(padding), child, key) { }

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderPadding(padding.Resolve(Directionality.Of(context)));

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderPadding)ro).Padding = padding.Resolve(Directionality.Of(context));
}

public interface IEdgeInsetsLike { EdgeInsets Resolve(TextDirection d); }
public sealed record PhysicalInsets(EdgeInsets Insets) : IEdgeInsetsLike { public EdgeInsets Resolve(TextDirection d) => Insets; }
public sealed record DirectionalInsets(EdgeInsetsDirectional Insets) : IEdgeInsetsLike { public EdgeInsets Resolve(TextDirection d) => Insets.Resolve(d); }

public sealed class Align(IAlignment? alignment = null, Widget? child = null, float? widthFactor = null, float? heightFactor = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? Alignment.Center).Resolve(Directionality.Of(c));

    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderPositionedBox(Resolve(context), widthFactor, heightFactor);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderPositionedBox)ro).Update(Resolve(context), widthFactor, heightFactor);
}

public sealed class Center(Widget? child = null, float? widthFactor = null, float? heightFactor = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Align(Alignment.Center, child, widthFactor, heightFactor);
}

public sealed class ConstrainedBox(BoxConstraints constraints, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderConstrainedBox(constraints);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderConstrainedBox)ro).AdditionalConstraints = constraints;
}

public sealed class SizedBox : StatelessWidget
{
    readonly float? _width, _height;
    readonly Widget? _child;

    public SizedBox(float? width = null, float? height = null, Widget? child = null, Key? key = null) : base(key)
    {
        _width = width; _height = height; _child = child;
    }

    public static SizedBox Expand(Widget? child = null) => new(float.PositiveInfinity, float.PositiveInfinity, child);
    public static SizedBox Shrink(Widget? child = null) => new(0, 0, child);
    public static SizedBox FromSize(Size size, Widget? child = null) => new(size.Width, size.Height, child);
    public static SizedBox Square(float dimension, Widget? child = null) => new(dimension, dimension, child);

    public override Widget Build(BuildContext context) =>
        new ConstrainedBox(BoxConstraints.TightFor(_width, _height), _child);
}

public sealed class LimitedBox(float maxWidth = float.PositiveInfinity, float maxHeight = float.PositiveInfinity, Widget? child = null, Key? key = null)
    : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLimitedBox(maxWidth, maxHeight);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderLimitedBox)ro).Update(maxWidth, maxHeight);
}

public sealed class RenderLimitedBox(float maxWidth, float maxHeight) : RenderProxyBox
{
    float _maxWidth = maxWidth, _maxHeight = maxHeight;

    public void Update(float w, float h)
    {
        if (_maxWidth == w && _maxHeight == h) return;
        _maxWidth = w; _maxHeight = h;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        var c = Constraints;
        var limited = c.Copy(
            maxWidth: c.HasBoundedWidth ? c.MaxWidth : Math.Clamp(_maxWidth, c.MinWidth, float.PositiveInfinity),
            maxHeight: c.HasBoundedHeight ? c.MaxHeight : Math.Clamp(_maxHeight, c.MinHeight, float.PositiveInfinity));
        if (Child is { } child)
        {
            child.Layout(limited);
            Size = c.Constrain(child.Size);
        }
        else Size = limited.Constrain(Size.Zero);
    }
}

public sealed class FractionallySizedBox(float? widthFactor = null, float? heightFactor = null, IAlignment? alignment = null, Widget? child = null, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Align(alignment ?? Alignment.Center, new FractionalBox(widthFactor, heightFactor, child));
}

sealed class FractionalBox(float? widthFactor, float? heightFactor, Widget? child) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFractionalBox(widthFactor, heightFactor);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFractionalBox)ro).Update(widthFactor, heightFactor);
}

sealed class RenderFractionalBox(float? widthFactor, float? heightFactor) : RenderProxyBox
{
    float? _w = widthFactor, _h = heightFactor;

    public void Update(float? w, float? h)
    {
        if (_w == w && _h == h) return;
        _w = w; _h = h;
        MarkNeedsLayout();
    }

    protected override void PerformLayout()
    {
        var c = Constraints;
        var inner = new BoxConstraints(
            _w is { } w ? c.MaxWidth * w : c.MinWidth, _w is { } w2 ? c.MaxWidth * w2 : c.MaxWidth,
            _h is { } h ? c.MaxHeight * h : c.MinHeight, _h is { } h2 ? c.MaxHeight * h2 : c.MaxHeight);
        if (Child is { } child)
        {
            child.Layout(inner.Enforce(c));
            Size = c.Constrain(child.Size);
        }
        else Size = c.Constrain(inner.Smallest);
    }
}

public sealed class IntrinsicWidth(Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderIntrinsic(Axis.Horizontal);
}

public sealed class IntrinsicHeight(Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderIntrinsic(Axis.Vertical);
}

sealed class RenderIntrinsic(Axis axis) : RenderProxyBox
{
    protected override void PerformLayout()
    {
        if (Child is not { } child) { Size = Constraints.Smallest; return; }
        var c = Constraints;
        if (axis == Axis.Horizontal)
        {
            float w = c.ConstrainWidth(child.MaxIntrinsicWidth(c.MaxHeight));
            child.Layout(c.Tighten(width: w));
        }
        else
        {
            float h = c.ConstrainHeight(child.MaxIntrinsicHeight(c.MaxWidth));
            child.Layout(c.Tighten(height: h));
        }
        Size = child.Size;
    }
}

public sealed class AspectRatio(float aspectRatio, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderAspectRatio(aspectRatio);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderAspectRatio)ro).Ratio = aspectRatio;
}

sealed class RenderAspectRatio(float ratio) : RenderProxyBox
{
    float _ratio = ratio;
    public float Ratio { set { if (_ratio == value) return; _ratio = value; MarkNeedsLayout(); } }

    protected override void PerformLayout()
    {
        var c = Constraints;
        float w = c.MaxWidth, h;
        if (float.IsFinite(w)) h = w / _ratio;
        else { h = c.MaxHeight; w = h * _ratio; }
        var size = c.Constrain(new Size(w, h));
        // Re-derive the other side when constraints clamped one of them.
        if (Math.Abs(size.Width / size.Height - _ratio) > 0.001f)
            size = size.Width / _ratio <= c.MaxHeight ? c.Constrain(new Size(size.Width, size.Width / _ratio)) : c.Constrain(new Size(size.Height * _ratio, size.Height));
        Child?.Layout(BoxConstraints.Tight(size));
        Size = size;
    }
}

// ---- painting ----

public sealed class DecoratedBox(BoxDecoration decoration, Widget? child = null, bool foreground = false, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderDecoratedBox(decoration, Directionality.Of(context), foreground);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderDecoratedBox)ro).Update(decoration, Directionality.Of(context));
}

public sealed class ColoredBox(SKColor color, Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new DecoratedBox(new BoxDecoration(Color: color), child);
}

public sealed class Opacity(float opacity, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderOpacity(opacity);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderOpacity)ro).Opacity = opacity;
}

public sealed class ClipRRect(BorderRadius borderRadius, Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderClip(borderRadius);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderClip)ro).Radius = borderRadius;
}

public sealed class ClipRect(Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderClip(null);
}

public sealed class ClipOval(Widget? child = null, Key? key = null) : StatelessWidget(key)
{
    // A circular clip is a rounded rect whose radius is half the shorter side; approximated via LayoutBuilder-free max radius.
    public override Widget Build(BuildContext context) => new ClipRRect(BorderRadius.Circular(10000), child);
}

public sealed class Transform(SKMatrix matrix, Widget? child = null, Alignment? origin = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public static Transform Rotate(float radians, Widget? child = null, Alignment? origin = null) =>
        new(SKMatrix.CreateRotation(radians), child, origin);
    public static Transform Scale(float scale, Widget? child = null, Alignment? origin = null) =>
        new(SKMatrix.CreateScale(scale, scale), child, origin);
    public static Transform Translate(Offset offset, Widget? child = null) =>
        new(SKMatrix.CreateTranslation(offset.Dx, offset.Dy), child);

    public override RenderObject CreateRenderObject(BuildContext context) => new RenderTransform(matrix, origin);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderTransform)ro).Update(matrix, origin);
}

// ---- Container: the all-in-one convenience widget ----

public sealed class Container(
    Widget? child = null,
    IAlignment? alignment = null,
    EdgeInsets? padding = null,
    SKColor? color = null,
    BoxDecoration? decoration = null,
    BoxDecoration? foregroundDecoration = null,
    float? width = null,
    float? height = null,
    BoxConstraints? constraints = null,
    EdgeInsets? margin = null,
    SKMatrix? transform = null,
    Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        if (color is not null && decoration is not null)
            throw new ArgumentException("Cannot provide both a color and a decoration; put the color inside the BoxDecoration.");

        var current = child;
        if (child is null && (constraints is null || !constraints.Value.IsTight))
            current = new LimitedBox(0, 0, new ConstrainedBox(BoxConstraints.Expand()));
        else if (alignment is not null)
            current = new Align(alignment, current);

        var effectivePadding = padding;
        var effectiveDecoration = decoration ?? (color is { } c ? new BoxDecoration(Color: c) : null);
        if (effectiveDecoration is not null && effectiveDecoration.Padding != EdgeInsets.Zero)
            effectivePadding = (effectivePadding ?? EdgeInsets.Zero) + effectiveDecoration.Padding;
        if (effectivePadding is { } p) current = new Padding(p, current);
        if (effectiveDecoration is not null) current = new DecoratedBox(effectiveDecoration, current);
        if (foregroundDecoration is not null) current = new DecoratedBox(foregroundDecoration, current, foreground: true);

        var effectiveConstraints = constraints;
        if (width is not null || height is not null)
            effectiveConstraints = (effectiveConstraints ?? new BoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity)).Tighten(width, height);
        if (effectiveConstraints is { } ec) current = new ConstrainedBox(ec, current);
        if (margin is { } m) current = new Padding(m, current);
        if (transform is { } t) current = new Transform(t, current);
        return current!;
    }
}

// ---- multi-child ----

public sealed class Flex(Axis direction, IReadOnlyList<Widget> children,
    MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start, MainAxisSize mainAxisSize = MainAxisSize.Max,
    CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Center, TextDirection? textDirection = null,
    VerticalDirection verticalDirection = VerticalDirection.Down, float spacing = 0, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderFlex(direction, mainAxisAlignment, mainAxisSize,
        crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection, spacing);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderFlex)ro).Update(direction, mainAxisAlignment,
        mainAxisSize, crossAxisAlignment, textDirection ?? Directionality.Of(context), verticalDirection, spacing);
}

public sealed class Row(IReadOnlyList<Widget> children, MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start,
    MainAxisSize mainAxisSize = MainAxisSize.Max, CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Center,
    TextDirection? textDirection = null, VerticalDirection verticalDirection = VerticalDirection.Down, float spacing = 0, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Flex(Axis.Horizontal, children, mainAxisAlignment, mainAxisSize,
        crossAxisAlignment, textDirection, verticalDirection, spacing);
}

public sealed class Column(IReadOnlyList<Widget> children, MainAxisAlignment mainAxisAlignment = MainAxisAlignment.Start,
    MainAxisSize mainAxisSize = MainAxisSize.Max, CrossAxisAlignment crossAxisAlignment = CrossAxisAlignment.Center,
    TextDirection? textDirection = null, VerticalDirection verticalDirection = VerticalDirection.Down, float spacing = 0, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Flex(Axis.Vertical, children, mainAxisAlignment, mainAxisSize,
        crossAxisAlignment, textDirection, verticalDirection, spacing);
}

public sealed class Flexible(Widget child, int flex = 1, FlexFit fit = FlexFit.Loose, Key? key = null) : ParentDataWidget(child, key)
{
    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderFlex;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not FlexParentData pd) ro.ParentData = pd = new FlexParentData();
        if (pd.Flex == flex && pd.Fit == fit) return;
        pd.Flex = flex; pd.Fit = fit;
        ro.Parent?.MarkNeedsLayout();
    }
}

public sealed class Expanded(Widget child, int flex = 1, Key? key = null) : ParentDataWidget(child, key)
{
    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderFlex;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not FlexParentData pd) ro.ParentData = pd = new FlexParentData();
        if (pd.Flex == flex && pd.Fit == FlexFit.Tight) return;
        pd.Flex = flex; pd.Fit = FlexFit.Tight;
        ro.Parent?.MarkNeedsLayout();
    }
}

public sealed class Spacer(int flex = 1, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Expanded(new SizedBox(), flex);
}

public sealed class Stack(IReadOnlyList<Widget> children, IAlignment? alignment = null, StackFit fit = StackFit.Loose,
    bool clip = true, Key? key = null) : MultiChildRenderObjectWidget(children, key)
{
    Alignment Resolve(BuildContext c) => (alignment ?? AlignmentDirectional.TopStart).Resolve(Directionality.Of(c));
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderStack(Resolve(context), fit, clip);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderStack)ro).Update(Resolve(context), fit, clip);
}

public sealed class Positioned(Widget child, float? left = null, float? top = null, float? right = null, float? bottom = null,
    float? width = null, float? height = null, Key? key = null) : ParentDataWidget(child, key)
{
    public static Positioned Fill(Widget child, float left = 0, float top = 0, float right = 0, float bottom = 0) =>
        new(child, left, top, right, bottom);

    public override bool DebugIsValidParent(RenderObject parent) => parent is RenderStack;
    public override void ApplyParentData(RenderObject ro)
    {
        if (ro.ParentData is not StackParentData pd) ro.ParentData = pd = new StackParentData();
        if (pd.Left == left && pd.Top == top && pd.Right == right && pd.Bottom == bottom && pd.Width == width && pd.Height == height) return;
        pd.Left = left; pd.Top = top; pd.Right = right; pd.Bottom = bottom; pd.Width = width; pd.Height = height;
        ro.Parent?.MarkNeedsLayout();
    }
}

/// <summary>Positioned with start/end edges resolved by the ambient text direction.</summary>
public sealed class PositionedDirectional(Widget child, float? start = null, float? top = null, float? end = null, float? bottom = null,
    float? width = null, float? height = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context)
    {
        bool rtl = Directionality.Of(context) == TextDirection.Rtl;
        return new Positioned(child, rtl ? end : start, top, rtl ? start : end, bottom, width, height);
    }
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A widget layered above the app (menus, dialogs, tooltips). Insert it with <see cref="OverlayState.Insert"/>.</summary>
public sealed class OverlayEntry(Func<BuildContext, Widget> builder)
{
    internal OverlayState? State;
    public Func<BuildContext, Widget> Builder { get; } = builder;

    public void Remove()
    {
        State?.Remove(this);
        State = null;
    }

    public void MarkNeedsBuild() => State?.Rebuild();
}

public sealed class Overlay(Widget initialChild) : StatefulWidget
{
    internal Widget Initial => initialChild;
    public override State CreateState() => new OverlayState();

    public static OverlayState? MaybeOf(BuildContext context) => context.Get<OverlayScope>()?.State;
    public static OverlayState Of(BuildContext context) =>
        MaybeOf(context) ?? throw new InvalidOperationException("No Overlay found above this context.");
}

sealed class OverlayScope(OverlayState state, Widget child) : InheritedWidget(child)
{
    public OverlayState State => state;
    public override bool UpdateShouldNotify(InheritedWidget old) => false;
}

public sealed class OverlayState : State<Overlay>
{
    readonly List<OverlayEntry> _entries = new();

    public void Insert(OverlayEntry entry)
    {
        entry.State = this;
        _entries.Add(entry);
        SetState();
    }

    internal void Remove(OverlayEntry entry)
    {
        if (_entries.Remove(entry) && Mounted) SetState();
    }

    internal void Rebuild() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context)
    {
        var children = new List<Widget> { new KeyedSubtree(new ValueKey<string>("overlay-initial"), Widget.Initial) };
        foreach (var e in _entries)
            children.Add(new KeyedSubtree(new ObjectKey(e), new Builder(e.Builder)));
        return new OverlayScope(this, new Stack(children, fit: StackFit.Expand, clip: false));
    }
}

/// <summary>Gives a subtree a key without changing how it builds.</summary>
public sealed class KeyedSubtree(Key key, Widget child) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => child;
}

/// <summary>Calls a function during build so the result can read a context that is below its parent.</summary>
public sealed class Builder(Func<BuildContext, Widget> builder, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => builder(context);
}

/// <summary>Draws directly onto the canvas.</summary>
public abstract class CustomPainter
{
    public abstract void Paint(SKCanvas canvas, Size size);
    public virtual bool ShouldRepaint(CustomPainter oldDelegate) => true;
    public virtual bool HitTest(Offset position) => false;
}

public sealed class CustomPaint(CustomPainter? painter = null, CustomPainter? foregroundPainter = null, Size? size = null,
    Widget? child = null, Key? key = null) : SingleChildRenderObjectWidget(child, key)
{
    public override RenderObject CreateRenderObject(BuildContext context) =>
        new RenderCustomPaint(painter, foregroundPainter, size ?? Size.Zero);

    public override void UpdateRenderObject(BuildContext context, RenderObject ro) =>
        ((RenderCustomPaint)ro).Update(painter, foregroundPainter, size ?? Size.Zero);
}

public sealed class RenderCustomPaint(CustomPainter? painter, CustomPainter? foreground, Size preferred) : RenderProxyBox
{
    CustomPainter? _painter = painter, _foreground = foreground;
    Size _preferred = preferred;

    public void Update(CustomPainter? painter, CustomPainter? foreground, Size preferred)
    {
        bool repaint = (painter is null) != (_painter is null) || (painter is not null && _painter is not null && painter.ShouldRepaint(_painter))
            || (foreground is null) != (_foreground is null) || (foreground is not null && _foreground is not null && foreground.ShouldRepaint(_foreground));
        _painter = painter; _foreground = foreground;
        if (_preferred != preferred) { _preferred = preferred; MarkNeedsLayout(); }
        if (repaint) MarkNeedsPaint();
    }

    protected override void PerformLayout()
    {
        if (Child is { } c) { c.Layout(Constraints); Size = c.Size; }
        else Size = Constraints.Constrain(_preferred);
    }

    public override void Paint(PaintingContext context, Offset offset)
    {
        void Draw(CustomPainter p)
        {
            context.Canvas.Save();
            context.Canvas.Translate(offset.Dx, offset.Dy);
            p.Paint(context.Canvas, Size);
            context.Canvas.Restore();
        }
        if (_painter is not null) Draw(_painter);
        base.Paint(context, offset);
        if (_foreground is not null) Draw(_foreground);
    }

    protected override bool HitTestSelf(Offset position) => _painter?.HitTest(position) ?? false;
}

using SkiaSharp;

namespace Sway.Widgets;

/// <summary>A <see cref="FractionallySizedBox"/> whose factors and alignment animate.</summary>
public sealed class AnimatedFractionallySizedBox(TimeSpan duration, Widget? child = null, float? widthFactor = null, float? heightFactor = null,
    Alignment? alignment = null, Curve? curve = null, Action? onEnd = null, Key? key = null) : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal float? WidthFactor => widthFactor;
    internal float? HeightFactor => heightFactor;
    internal Alignment Alignment => alignment ?? Alignment.Center;
    public override State CreateState() => new AnimatedFractionallySizedBoxState();
}

sealed class AnimatedFractionallySizedBoxState : ImplicitlyAnimatedWidgetState<AnimatedFractionallySizedBox>
{
    Tween<float>? _width, _height;
    Tween<Alignment>? _alignment;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _width = v.VisitValue(_width, Widget.WidthFactor, a => new FloatTween(a, a));
        _height = v.VisitValue(_height, Widget.HeightFactor, a => new FloatTween(a, a));
        _alignment = v.VisitValue(_alignment, (Alignment?)Widget.Alignment, a => new AlignmentTween(a, a));
    }

    public override Widget Build(BuildContext context) =>
        new FractionallySizedBox(_width?.Evaluate(Animation), _height?.Evaluate(Animation), _alignment!.Evaluate(Animation), Widget.Child);
}

/// <summary>A clipped, coloured, elevated surface whose colour, elevation and corner radius animate.</summary>
public sealed class AnimatedPhysicalModel(TimeSpan duration, Widget? child = null, SKColor? color = null, float elevation = 0,
    BorderRadius? borderRadius = null, SKColor? shadowColor = null, Curve? curve = null, Action? onEnd = null, Key? key = null)
    : ImplicitlyAnimatedWidget(duration, curve, onEnd, key)
{
    internal Widget? Child => child;
    internal SKColor? Color => color;
    internal float Elevation => elevation;
    internal BorderRadius Radius => borderRadius ?? BorderRadius.Zero;
    internal SKColor? ShadowColor => shadowColor;
    public override State CreateState() => new AnimatedPhysicalModelState();
}

sealed class AnimatedPhysicalModelState : ImplicitlyAnimatedWidgetState<AnimatedPhysicalModel>
{
    Tween<SKColor>? _color, _shadow;
    Tween<float>? _elevation;
    Tween<BorderRadius>? _radius;

    protected override void ForEachTween(ITweenVisitor v)
    {
        _color = v.VisitValue(_color, Widget.Color, a => new ColorTween(a, a));
        _shadow = v.VisitValue(_shadow, Widget.ShadowColor, a => new ColorTween(a, a));
        _elevation = v.VisitValue(_elevation, (float?)Widget.Elevation, a => new FloatTween(a, a));
        _radius = v.VisitValue(_radius, (BorderRadius?)Widget.Radius, a => new BorderRadiusTween(a, a));
    }

    /// <summary>Material elevation levels are discrete, so fractional elevations blend the two neighbouring levels.</summary>
    static IReadOnlyList<BoxShadow>? ShadowsFor(float elevation, SKColor shadow)
    {
        int low = (int)MathF.Floor(elevation);
        float t = elevation - low;
        var a = Elevation.Shadows(low, shadow);
        return t < 0.001f ? a : Lerps.BoxShadows(a, Elevation.Shadows(low + 1, shadow), t);
    }

    public override Widget Build(BuildContext context)
    {
        var radius = _radius!.Evaluate(Animation);
        Widget inner = Widget.Child ?? new SizedBox();
        if (!radius.IsZero) inner = new ClipRRect(radius, inner);
        return new DecoratedBox(new BoxDecoration(
            Color: _color?.Evaluate(Animation) ?? Colors.Transparent,
            BorderRadius: radius,
            BoxShadow: ShadowsFor(_elevation!.Evaluate(Animation), _shadow?.Evaluate(Animation) ?? Colors.Black)), inner);
    }
}

/// <summary>Clips and sizes its child along one axis by an animation, like a drawer opening.</summary>
public sealed class SizeTransition(Animation<float> sizeFactor, Widget? child = null, Axis axis = Axis.Vertical, float axisAlignment = 0, Key? key = null)
    : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new AnimatedBuilder(sizeFactor, (_, inner) =>
    {
        float factor = Math.Max(0, sizeFactor.Value);
        bool vertical = axis == Axis.Vertical;
        return new ClipRect(new Align(
            vertical ? new Alignment(0, axisAlignment) : new Alignment(axisAlignment, 0), inner,
            widthFactor: vertical ? null : factor, heightFactor: vertical ? factor : null));
    }, child);
}

/// <summary>Drives <see cref="AnimatedList"/>: insert and remove items with an animation.</summary>
public sealed class AnimatedListController
{
    internal AnimatedListState? State;

    public int Count => State?.LiveCount ?? 0;

    /// <summary>Animates in the item that the list's builder now returns for <paramref name="index"/>.</summary>
    public void InsertItem(int index, TimeSpan? duration = null) => Attached.Insert(index, duration);

    /// <summary>
    /// Animates out the item at <paramref name="index"/>. <paramref name="removedBuilder"/> must draw the item as it was
    /// (the list's builder no longer has it); its animation runs from 1 to 0.
    /// </summary>
    public void RemoveItem(int index, Func<BuildContext, Animation<float>, Widget> removedBuilder, TimeSpan? duration = null) =>
        Attached.Remove(index, removedBuilder, duration);

    AnimatedListState Attached => State ?? throw new InvalidOperationException("The controller is not attached to an AnimatedList.");
}

/// <summary>
/// A list that animates items in and out. Keep your data in sync: call <c>controller.InsertItem(i)</c> after adding to your
/// collection and <c>controller.RemoveItem(i, ...)</c> after removing from it.
/// </summary>
public sealed class AnimatedList(AnimatedListController controller, int initialItemCount, Func<BuildContext, int, Animation<float>, Widget> itemBuilder,
    Axis scrollDirection = Axis.Vertical, EdgeInsets? padding = null, ScrollController? scrollController = null, Key? key = null) : StatefulWidget(key)
{
    internal AnimatedListController Controller => controller;
    internal int InitialItemCount => initialItemCount;
    internal Func<BuildContext, int, Animation<float>, Widget> ItemBuilder => itemBuilder;
    internal Axis ScrollDirection => scrollDirection;
    internal EdgeInsets? Padding => padding;
    internal ScrollController? ScrollController => scrollController;
    public override State CreateState() => new AnimatedListState();
}

sealed class AnimatedListState : TickerProviderState<AnimatedList>
{
    static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(300);

    sealed class Entry(int id, AnimationController controller)
    {
        public readonly int Id = id;
        public readonly AnimationController Controller = controller;
        public Func<BuildContext, Animation<float>, Widget>? RemovedBuilder;
        public bool Removing => RemovedBuilder is not null;
    }

    readonly List<Entry> _entries = new();
    int _nextId;

    public int LiveCount => _entries.Count(e => !e.Removing);

    public override void InitState()
    {
        Widget.Controller.State = this;
        for (int i = 0; i < Widget.InitialItemCount; i++)
            _entries.Add(new Entry(_nextId++, new AnimationController(this, DefaultDuration, value: 1)));
    }

    public override void DidUpdateWidget(AnimatedList old)
    {
        if (ReferenceEquals(old.Controller, Widget.Controller)) return;
        if (ReferenceEquals(old.Controller.State, this)) old.Controller.State = null;
        Widget.Controller.State = this;
    }

    public override void Dispose()
    {
        if (ReferenceEquals(Widget.Controller.State, this)) Widget.Controller.State = null;
        foreach (var e in _entries) e.Controller.Dispose();
        base.Dispose();
    }

    int EntryIndexOfLive(int liveIndex)
    {
        int live = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Removing) continue;
            if (live == liveIndex) return i;
            live++;
        }
        return live == liveIndex ? _entries.Count : throw new ArgumentOutOfRangeException(nameof(liveIndex));
    }

    public void Insert(int index, TimeSpan? duration)
    {
        if (index < 0 || index > LiveCount) throw new ArgumentOutOfRangeException(nameof(index));
        var controller = new AnimationController(this, duration ?? DefaultDuration);
        _entries.Insert(EntryIndexOfLive(index), new Entry(_nextId++, controller));
        controller.Forward();
        SetState();
    }

    public void Remove(int index, Func<BuildContext, Animation<float>, Widget> removedBuilder, TimeSpan? duration)
    {
        if (index < 0 || index >= LiveCount) throw new ArgumentOutOfRangeException(nameof(index));
        var entry = _entries[EntryIndexOfLive(index)];
        entry.RemovedBuilder = removedBuilder;
        entry.Controller.Duration = duration ?? entry.Controller.Duration ?? DefaultDuration;
        entry.Controller.AddStatusListener(status =>
        {
            if (status != AnimationStatus.Dismissed || !_entries.Remove(entry)) return;
            entry.Controller.Dispose();
            if (Mounted) SetState();
        });
        entry.Controller.Reverse(from: 1);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var children = new List<Widget>(_entries.Count);
        int live = 0;
        foreach (var entry in _entries)
        {
            int index = live; // captured by value: the builder runs after this loop has moved on
            Widget child = entry.RemovedBuilder is { } removed
                ? new Builder(ctx => removed(ctx, entry.Controller))
                : new Builder(ctx => Widget.ItemBuilder(ctx, index, entry.Controller));
            if (!entry.Removing) live++;
            children.Add(new KeyedSubtree(new ValueKey<int>(entry.Id), child));
        }
        return new ListView(children, Widget.ScrollDirection, Widget.ScrollController, Widget.Padding);
    }
}

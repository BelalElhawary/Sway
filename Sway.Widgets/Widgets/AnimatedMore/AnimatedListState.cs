using SkiaSharp;

namespace Sway.Widgets;

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

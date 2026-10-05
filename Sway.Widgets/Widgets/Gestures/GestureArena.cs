namespace Sway.Widgets;

/// <summary>
/// Resolves which of several competing recognizers (tap vs drag...) owns a pointer.
/// The first member to claim it wins; if nobody claims, the deepest member wins on pointer-up.
/// </summary>
public sealed class GestureArena
{
    sealed class Entry
    {
        public readonly List<IGestureArenaMember> Members = new();
        public bool Closed;
        public IGestureArenaMember? EagerWinner;
    }

    readonly Dictionary<int, Entry> _arenas = new();

    public void Add(int pointer, IGestureArenaMember member)
    {
        if (!_arenas.TryGetValue(pointer, out var e)) _arenas[pointer] = e = new Entry();
        e.Members.Add(member);
    }

    /// <summary>No more members will join; a lone survivor wins immediately.</summary>
    public void Close(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var e)) return;
        e.Closed = true;
        TryResolveLast(pointer, e);
    }

    /// <summary>Pointer-up with no claimant: the first (deepest) member wins and the rest lose.</summary>
    public void Sweep(int pointer)
    {
        if (!_arenas.Remove(pointer, out var e) || e.Members.Count == 0) return;
        var winner = e.EagerWinner ?? e.Members[0];
        foreach (var m in e.Members.ToArray())
            if (!ReferenceEquals(m, winner)) m.RejectGesture(pointer);
        winner.AcceptGesture(pointer);
    }

    public void Resolve(int pointer, IGestureArenaMember member, bool accepted)
    {
        if (!_arenas.TryGetValue(pointer, out var e) || !e.Members.Contains(member)) return;

        if (!accepted)
        {
            e.Members.Remove(member);
            member.RejectGesture(pointer);
            if (e.Members.Count == 0) _arenas.Remove(pointer);
            else TryResolveLast(pointer, e);
            return;
        }

        _arenas.Remove(pointer);
        foreach (var m in e.Members.ToArray())
            if (!ReferenceEquals(m, member)) m.RejectGesture(pointer);
        member.AcceptGesture(pointer);
    }

    void TryResolveLast(int pointer, Entry e)
    {
        if (!e.Closed || e.Members.Count != 1) return;
        _arenas.Remove(pointer);
        e.Members[0].AcceptGesture(pointer);
    }
}

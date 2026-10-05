namespace Sway.Widgets;

/// <summary>Delivers raw pointer events by pointer id, independent of where the pointer currently is.</summary>
public sealed class PointerRouter
{
    readonly Dictionary<int, List<Action<PointerEvent>>> _routes = new();

    public void AddRoute(int pointer, Action<PointerEvent> handler)
    {
        if (!_routes.TryGetValue(pointer, out var list)) _routes[pointer] = list = new();
        list.Add(handler);
    }

    public void RemoveRoute(int pointer, Action<PointerEvent> handler)
    {
        if (_routes.TryGetValue(pointer, out var list)) list.Remove(handler);
    }

    public void Route(PointerEvent e)
    {
        if (!_routes.TryGetValue(e.Pointer, out var list)) return;
        foreach (var h in list.ToArray()) h(e);
        if (e.Kind is PointerEventKind.Up or PointerEventKind.Cancel) _routes.Remove(e.Pointer);
    }
}

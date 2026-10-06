namespace Sway.Widgets;

/// <summary>The active <see cref="ILocationSource"/>. Without a platform package it is an in-memory history that starts at <c>/</c>.</summary>
public static class AppLocation
{
    static ILocationSource? _source;

    /// <summary>Set by the platform host at startup, before the root widget is attached.</summary>
    public static ILocationSource Source
    {
        get => _source ??= new MemoryLocationSource();
        set => _source = value;
    }
}

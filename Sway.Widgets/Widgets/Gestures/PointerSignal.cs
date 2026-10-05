namespace Sway.Widgets;

/// <summary>Lets the innermost scrollable consume a wheel event so outer ones do not also scroll.</summary>
public static class PointerSignal
{
    public static bool Handled { get; private set; }
    public static void Reset() => Handled = false;
    public static void Consume() => Handled = true;
}

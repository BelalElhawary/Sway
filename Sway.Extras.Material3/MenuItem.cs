using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>A selectable row in a popup menu.</summary>
public sealed record MenuItem<T>(T Value, Widget Child, bool Enabled = true, IconData? Icon = null) : MenuEntry<T>;

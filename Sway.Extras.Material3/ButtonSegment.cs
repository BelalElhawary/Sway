using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed record ButtonSegment<T>(T Value, string? Label = null, IconData? Icon = null, bool Enabled = true);

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>One panel of an <see cref="ExpansionPanelList"/>.</summary>
public sealed record ExpansionPanel(Widget Header, Widget Body, bool IsExpanded = false, bool CanTapOnHeader = true);

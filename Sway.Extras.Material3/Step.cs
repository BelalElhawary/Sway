using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>One step of a <see cref="Stepper"/>. The content is shown only while the step is the current one.</summary>
public sealed record Step(Widget Title, Widget Content, Widget? Subtitle = null, StepState State = StepState.Indexed, bool IsActive = false);

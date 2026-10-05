using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class FilledButton(Widget child, Action? onPressed = null, IconData? icon = null, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Button(child, onPressed, ButtonVariant.Filled, color, icon);
}

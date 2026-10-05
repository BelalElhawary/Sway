using SkiaSharp;

namespace Sway.Widgets;

public sealed class IconTheme(SKColor? color, float? size, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public SKColor? Color { get; } = color;
    public float? Size { get; } = size;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((IconTheme)old).Color != Color || ((IconTheme)old).Size != Size;
    public static IconTheme? Of(BuildContext context) => context.DependOn<IconTheme>();
}

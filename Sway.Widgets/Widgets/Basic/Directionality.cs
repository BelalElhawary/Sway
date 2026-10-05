using SkiaSharp;

namespace Sway.Widgets;

public sealed class Directionality(TextDirection textDirection, Widget child, Key? key = null) : InheritedWidget(child, key)
{
    public TextDirection TextDirection { get; } = textDirection;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((Directionality)old).TextDirection != TextDirection;

    public static TextDirection Of(BuildContext context) => context.DependOn<Directionality>()?.TextDirection ?? TextDirection.Ltr;
}

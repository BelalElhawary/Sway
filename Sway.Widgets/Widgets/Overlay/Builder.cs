using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Calls a function during build so the result can read a context that is below its parent.</summary>
public sealed class Builder(Func<BuildContext, Widget> builder, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => builder(context);
}

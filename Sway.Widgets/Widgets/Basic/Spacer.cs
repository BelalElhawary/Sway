using SkiaSharp;

namespace Sway.Widgets;

public sealed class Spacer(int flex = 1, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new Expanded(new SizedBox(), flex);
}

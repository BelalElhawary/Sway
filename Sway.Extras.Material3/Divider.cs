using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

public sealed class Divider(float height = 16, float thickness = 1, float indent = 0, float endIndent = 0, SKColor? color = null, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new SizedBox(height: height, child: new Center(
        new Container(height: thickness, margin: EdgeInsets.Only(left: indent, right: endIndent),
            color: color ?? Theme.Of(context).ColorScheme.OutlineVariant)));
}

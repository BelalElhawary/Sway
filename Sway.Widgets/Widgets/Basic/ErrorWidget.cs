using SkiaSharp;

namespace Sway.Widgets;

public sealed class ErrorWidget(string message) : StatelessWidget
{
    public override Widget Build(BuildContext context) =>
        new ColoredBox(new SKColor(0xFF, 0xE0, 0xE0), new Padding(EdgeInsets.All(8),
            new Text(message, style: new TextStyle(Color: Colors.Red, FontSize: 12))));
}

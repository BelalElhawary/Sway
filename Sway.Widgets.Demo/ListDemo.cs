using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class ListDemo : StatelessWidget
{
    public override Widget Build(BuildContext context) =>
        ListView.Builder(1500, (ctx, i) => new Container(
            padding: EdgeInsets.Symmetric(16, 0),
            alignment: Alignment.CenterLeft,
            decoration: new BoxDecoration(Color: i % 2 == 0 ? Colors.White : Colors.FromRgb(0xF3F4F6)),
            child: new Text($"Row {i}", style: new TextStyle(FontWeight: i % 100 == 0 ? FontWeight.Bold : FontWeight.Normal))), itemExtent: 36);
}

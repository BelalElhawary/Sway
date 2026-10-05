using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

public enum TagColor { Gray, Red, Magenta, Purple, Blue, Cyan, Teal, Green, Outline }

/// <summary>A Carbon tag: a small pill that labels or categorises something. Optionally dismissible. <paramref name="compact"/> makes it 18px tall, for dense table rows.</summary>
public sealed class Tag(string label, TagColor color = TagColor.Gray, Action? onClose = null, bool compact = false, Key? key = null) : StatelessWidget(key)
{
    // Carbon tag tokens: (light background, light text, dark background, dark text).
    static (uint Back, uint Text, uint DarkBack, uint DarkText) Palette(TagColor c) => c switch
    {
        TagColor.Red => (0xFFD7D9, 0xA2191F, 0x750E13, 0xFFB3B8),
        TagColor.Magenta => (0xFFD6E8, 0x9F1853, 0x510224, 0xFFAFD2),
        TagColor.Purple => (0xE8DAFF, 0x6929C4, 0x491D8B, 0xD4BBFF),
        TagColor.Blue => (0xD0E2FF, 0x0043CE, 0x002D9C, 0xA6C8FF),
        TagColor.Cyan => (0xBAE6FF, 0x00539A, 0x012749, 0x82CFFF),
        TagColor.Teal => (0x9EF0F0, 0x005D5D, 0x022B30, 0x3DDBD9),
        TagColor.Green => (0xA7F0BA, 0x0E6027, 0x044317, 0x6FDC8C),
        _ => (0xE0E0E0, 0x161616, 0x393939, 0xC6C6C6),
    };

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        bool dark = s.Brightness == Brightness.Dark;
        SKColor back, fore;
        Border? border = null;
        if (color == TagColor.Outline)
        {
            back = Colors.Transparent; fore = s.OnSurface; border = Border.All(s.Outline);
        }
        else
        {
            var p = Palette(color);
            back = Colors.FromRgb(dark ? p.DarkBack : p.Back);
            fore = Colors.FromRgb(dark ? p.DarkText : p.Text);
        }

        Widget text = new Text(label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
            style: (compact ? theme.TextTheme.LabelSmall : theme.TextTheme.LabelMedium).Merge(new TextStyle(Color: fore)));
        Widget content = onClose is null
            ? text
            : new Row(mainAxisSize: MainAxisSize.Min, spacing: 4, children:
            [
                text,
                new GestureDetector(onTap: onClose, behavior: HitTestBehavior.Opaque, child: new Icon(Icons.Close, compact ? 12 : 14, fore)),
            ]);
        // widthFactor makes the Center shrink-wrap the label; an alignment on the Container would stretch the tag to fill its parent.
        float height = compact ? 18 : 24;
        return new Container(height: height, padding: EdgeInsets.Symmetric(horizontal: compact ? 6 : 8),
            decoration: new BoxDecoration(Color: back, BorderRadius: BorderRadius.Circular(height / 2), Border: border), child: new Center(content, 1));
    }
}

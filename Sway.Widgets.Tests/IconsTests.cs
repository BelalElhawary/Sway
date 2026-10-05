using System.Reflection;
using SkiaSharp;
using Xunit;

namespace Sway.Widgets.Tests;

public class IconsTests
{
    static IEnumerable<(string name, IconData icon)> All() => Of(typeof(Icons));

    static IEnumerable<(string name, IconData icon)> Of(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.PropertyType == typeof(IconData))
            .Select(f => (f.Name, (IconData)f.GetValue(null)!));

    [Fact]
    public void EveryIconParsesAndFitsTheGrid()
    {
        foreach (var (name, icon) in All())
        {
            using var path = SKPath.ParseSvgPathData(icon.Path);
            Assert.True(path is not null, $"{name} does not parse");
            var b = path!.TightBounds;
            Assert.True(b.Width >= 2 && b.Height >= 2, $"{name} is empty or tiny: {b}");
            Assert.True(b.Left >= -2f && b.Top >= -2f && b.Right <= 26f && b.Bottom <= 26f, $"{name} leaves the 24x24 grid: {b}");
        }
    }

    [Fact]
    public void IconNamesAreUnique()
    {
        var names = All().Select(i => i.name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.True(names.Count >= 2000);
    }

    [Fact]
    public void EveryStyleMirrorsTheFilledSet()
    {
        var filled = All().Select(i => i.name).ToHashSet();
        foreach (var style in new[] { typeof(Icons.Outlined), typeof(Icons.Sharp), typeof(Icons.TwoTone) })
        {
            var icons = Of(style).ToList();
            Assert.True(filled.SetEquals(icons.Select(i => i.name)), style.Name);
            foreach (var (name, icon) in icons)
            {
                using var path = SKPath.ParseSvgPathData(icon.Path);
                Assert.True(path is not null, $"{style.Name}.{name} does not parse");
                if (icon.Secondary is not null)
                {
                    using var second = SKPath.ParseSvgPathData(icon.Secondary);
                    Assert.True(second is not null, $"{style.Name}.{name} secondary does not parse");
                }
            }
        }
        Assert.NotEqual(Icons.Home.Path, Icons.Outlined.Home.Path);
        Assert.NotEqual(Icons.Home.Path, Icons.Sharp.Home.Path);
        Assert.NotNull(Icons.TwoTone.Home.Secondary);
        Assert.Null(Icons.Home.Secondary);
    }

    [Fact]
    public void ContactSheet()
    {
        string? target = Environment.GetEnvironmentVariable("SWAY_ICON_SHEET");
        if (string.IsNullOrEmpty(target)) return;

        var icons = All().ToList();
        const int cell = 96, cols = 10;
        int rows = (icons.Count + cols - 1) / cols;
        using var bitmap = new SKBitmap(cols * cell, rows * cell);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var label = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
        using var font = new SKFont { Size = 9 };
        for (int i = 0; i < icons.Count; i++)
        {
            int x = i % cols * cell, y = i / cols * cell;
            using var path = SKPath.ParseSvgPathData(icons[i].icon.Path)!;
            canvas.Save();
            canvas.Translate(x + 24, y + 8);
            canvas.Scale(2);
            using var fill = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawPath(path, fill);
            canvas.Restore();
            canvas.DrawText(icons[i].name, x + 4, y + cell - 6, font, label);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(target, data.ToArray());
    }
}

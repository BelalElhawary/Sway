using SkiaSharp;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sway.Widgets.Tests;

/// <summary>A headless app on a manual clock; tests drive pointer input and time directly.</summary>
sealed class Harness
{
    public WidgetsBinding Binding { get; } = new();
    public int Width { get; }
    public int Height { get; }

    public Harness(Widget root, int width = 400, int height = 300)
    {
        Width = width; Height = height;
        Binding.UseManualClock();
        Binding.AttachRoot(root);
        Pump();
    }

    public GestureBinding Gestures => Binding.Gestures;

    public void Pump()
    {
        using var bitmap = Binding.RenderToBitmap(Width, Height);
    }

    public void Advance(int milliseconds)
    {
        Binding.AdvanceClock(TimeSpan.FromMilliseconds(milliseconds));
        Pump();
    }

    /// <summary>All render objects of type <typeparamref name="T"/> in the tree, parents before children.</summary>
    public List<T> Find<T>() where T : RenderObject
    {
        var found = new List<T>();
        void Visit(RenderObject o)
        {
            if (o is T t) found.Add(t);
            o.VisitChildren(Visit);
        }
        Visit(Binding.RenderView);
        return found;
    }

    /// <summary>Renders the current frame to a bitmap the caller owns.</summary>
    public SKBitmap Render() => Binding.RenderToBitmap(Width, Height);

    /// <summary>Renders the current frame to a PNG, for looking at what a test built.</summary>
    public void Save(string path)
    {
        using var bitmap = Binding.RenderToBitmap(Width, Height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    public void Tap(float x, float y)
    {
        Gestures.PointerDown(x, y);
        Gestures.PointerUp(x, y);
        Pump();
    }
}

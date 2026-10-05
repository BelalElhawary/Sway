using SkiaSharp;

namespace Sway.Widgets;

/// <summary>Renders widgets without a window or a platform host, using a software surface.</summary>
public static class Headless
{
    /// <summary>
    /// Renders the widget to a PNG. Each step simulates input against a freshly laid-out frame,
    /// then a new screenshot overwrites the file.
    /// </summary>
    public static void Screenshot(Widget root, string pngPath, int width, int height, IEnumerable<Action<WidgetsBinding>>? steps = null)
    {
        var binding = new WidgetsBinding();
        binding.UseManualClock();
        binding.AttachRoot(root);
        SavePng(binding, pngPath, width, height);

        foreach (var step in steps ?? Enumerable.Empty<Action<WidgetsBinding>>())
        {
            step(binding);
            SavePng(binding, pngPath, width, height);
        }
    }

    static void SavePng(WidgetsBinding binding, string path, int width, int height)
    {
        using var bitmap = binding.RenderToBitmap(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}

using System.Diagnostics;
using SkiaSharp;
using Sway.Media;
using Sway.Widgets;
using Sway.Example;

// Renders the example without a window. `--screenshot out.png` writes a PNG. Scripted steps run in order before each capture:
//   --move x,y  --click x,y  --wheel x,y,delta  --type text  --key [ctrl+][shift+]Name  --advance ms
// Options: --page components|layout|forms|motion|effects|rtl|stress|media|icons  --dark  --rtl  --carbon  --size WxH  --bench
LibVlcMediaBackend.Install();
int shot = Array.IndexOf(args, "--screenshot");
bool bench = args.Contains("--bench");
if ((shot < 0 || shot + 1 >= args.Length) && !bench)
{
    Console.WriteLine("Usage: --screenshot out.png [options] | --bench [options]");
    return;
}

var steps = new List<Action<WidgetsBinding>>();
string page = "components";
bool dark = false, rtl = false, carbon = false;
int width = 1100, height = 760;
for (int i = 0; i < args.Length; i++)
{
    float[] Numbers(string s) => s.Split(',').Select(float.Parse).ToArray();
    switch (args[i])
    {
        case "--page": page = args[++i]; break;
        case "--dark": dark = true; break;
        case "--rtl": rtl = true; break;
        case "--carbon": carbon = true; break;
        case "--size": { var p = args[++i].Split('x'); width = int.Parse(p[0]); height = int.Parse(p[1]); break; }
        case "--move": { var n = Numbers(args[++i]); steps.Add(b => b.Gestures.PointerMove(n[0], n[1])); break; }
        case "--click":
        {
            var n = Numbers(args[++i]);
            steps.Add(b => { b.Gestures.PointerMove(n[0], n[1]); b.Gestures.PointerDown(n[0], n[1]); b.Gestures.PointerUp(n[0], n[1]); });
            break;
        }
        case "--wheel": { var n = Numbers(args[++i]); steps.Add(b => b.Gestures.PointerScroll(n[0], n[1], 0, n[2])); break; }
        case "--type": { string text = args[++i]; steps.Add(b => b.TextInput(text)); break; }
        case "--key":
        {
            var parts = args[++i].Split('+');
            string key = parts[^1];
            var mods = parts[..^1].Select(m => m.ToLowerInvariant()).ToHashSet();
            steps.Add(b =>
            {
                b.Ctrl = mods.Contains("ctrl"); b.Shift = mods.Contains("shift");
                b.KeyDown(key, key); b.KeyUp(key, key);
                b.Ctrl = b.Shift = false;
            });
            break;
        }
        case "--advance": { float ms = float.Parse(args[++i]); steps.Add(b => b.AdvanceClock(TimeSpan.FromMilliseconds(ms))); break; }
    }
}

if (bench)
{
    // Cold start, then scroll the page and report per-frame cost.
    var binding = new WidgetsBinding();
    binding.UseManualClock();
    var sw = Stopwatch.StartNew();
    binding.AttachRoot(new DemoRoot(page, dark, rtl, carbon));
    using (var first = binding.RenderToBitmap(width, height)) { }
    Console.WriteLine($"cold start (mount + first frame): {sw.ElapsedMilliseconds} ms");

    var times = new List<double>();
    for (int i = 0; i < 60; i++)
    {
        binding.Gestures.PointerScroll(width / 2f, height / 2f, 0, 40);
        sw.Restart();
        using var bmp = binding.RenderToBitmap(width, height);
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    Console.WriteLine($"scroll frame (CPU raster): avg {times.Average():F1} ms, max {times.Max():F1} ms");

    times.Clear();
    for (int i = 0; i < 60; i++)
    {
        binding.Gestures.PointerMove(100 + i * 5, 200 + i * 3);
        sw.Restart();
        using var bmp = binding.RenderToBitmap(width, height);
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    Console.WriteLine($"hover frame (CPU raster): avg {times.Average():F1} ms, max {times.Max():F1} ms");
    return;
}

Headless.Screenshot(new DemoRoot(page, dark, rtl, carbon), args[shot + 1], width, height, steps);

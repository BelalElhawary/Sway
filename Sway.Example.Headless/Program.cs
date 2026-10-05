using System.Diagnostics;
using SkiaSharp;
using Sway.Media;
using Sway.Widgets;
using Sway.Example;

// Renders the example without a window. `--screenshot out.png` writes a PNG. Scripted steps run in order before each capture:
//   --move x,y  --click x,y  --wheel x,y,delta  --type text  --key [ctrl+][shift+]Name  --advance ms
// Options: --page material|layout|forms|motion|effects|rtl|stress|media|icons|carbon  --dark  --rtl  --size WxH  --bench
LibVlcMediaBackend.Install();
int shot = Array.IndexOf(args, "--screenshot");
bool bench = args.Contains("--bench");
if ((shot < 0 || shot + 1 >= args.Length) && !bench)
{
    Console.WriteLine("Usage: --screenshot out.png [options] | --bench [options]");
    return;
}

var steps = new List<Action<WidgetsBinding>>();
string page = "material";
bool dark = false, rtl = false;
int width = 1100, height = 760;
for (int i = 0; i < args.Length; i++)
{
    float[] Numbers(string s) => s.Split(',').Select(float.Parse).ToArray();
    switch (args[i])
    {
        case "--page": page = args[++i]; break;
        case "--dark": dark = true; break;
        case "--rtl": rtl = true; break;
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
    binding.AttachRoot(new DemoRoot(page, dark, rtl));
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

    // Optimizations 1.1 / 4.1 on a static synthetic page (the demo pages all animate, so they never go idle):
    // 60 rows, each a hoverable region on the left half and plain space on the right.
    var rows = Enumerable.Range(0, 60).Select(r => (Widget)new SizedBox(width, 12, new Row(new Widget[]
    {
        new SizedBox(width / 2f, 12, new MouseRegion(new Text($"Row {r} " + new string('x', 40)), cursor: MouseCursor.Click)),
    }))).ToList();
    var hb = new WidgetsBinding();
    hb.UseManualClock();
    hb.AttachRoot(new Column(rows));
    using (var settle = hb.RenderToBitmap(width, height)) { }

    // The windowed host only draws when NeedsFrame is true; model that loop. Moves stay inside one row's region,
    // then travel through plain space, so after the first enter/leave nothing changes.
    const int moves = 1200;
    int drawn = 0;
    sw.Restart();
    for (int i = 0; i < moves; i++)
    {
        hb.Gestures.PointerMove(i < 600 ? 10 + i % 400 : width / 2f + 10 + i % 400, 5);
        if (!hb.NeedsFrame(width, height)) continue;
        using var bmp = hb.RenderToBitmap(width, height);
        drawn++;
    }
    Console.WriteLine($"hover host loop: {moves} moves, {drawn} frames drawn, {sw.Elapsed.TotalMilliseconds / moves:F3} ms per move");

    // Clock-only frames with a still pointer (caret blink, animation tick): AfterFrame's hit test shows here.
    hb.Gestures.PointerMove(20, 5);
    times.Clear();
    for (int i = 0; i < 300; i++)
    {
        hb.AdvanceClock(TimeSpan.FromMilliseconds(16));
        sw.Restart();
        using var bmp = hb.RenderToBitmap(width, height);
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    Console.WriteLine($"still-pointer frame (CPU raster): avg {times.Average():F3} ms, max {times.Max():F3} ms");

    // Optimization 2.1: grapheme boundary lookups on a 5k-character field (FitLine, IndexAt and caret moves call these per character).
    foreach (var (label, text) in new[]
    {
        ("ascii", string.Concat(Enumerable.Repeat("hello world, ", 385))),
        ("combining", string.Concat(Enumerable.Repeat("hellö wórld 😀 ", 200))),
    })
    {
        var edit = new TextEditState(text, true);
        int walked = 0;
        sw.Restart();
        for (int i = 0; i < edit.Value.Length; i = edit.NextBoundary(i)) walked++;
        for (int i = edit.Value.Length; i > 0; i = edit.PreviousBoundary(i)) walked++;
        Console.WriteLine($"grapheme walk, {label} ({edit.Value.Length} chars, {walked} steps): {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    // Optimization 2.2 / 2.3: typing into a 5k-character textarea (every keystroke re-lays-out the field).
    {
        var area = new WidgetsBinding();
        area.UseManualClock();
        var controller = new TextEditingController(string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 111)));
        area.AttachRoot(new Align(Alignment.TopLeft, new Directionality(TextDirection.Ltr,
            new SizedBox(width: 400, child: new EditableText(controller, maxLines: null, autofocus: true)))));
        sw.Restart();
        using (var first = area.RenderToBitmap(width, height)) { }
        Console.WriteLine($"textarea first layout, {controller.Text.Length} chars: {sw.Elapsed.TotalMilliseconds:F1} ms");
        area.Gestures.PointerDown(20, 10); area.Gestures.PointerUp(20, 10);
        using (var focused = area.RenderToBitmap(width, height)) { }
        times.Clear();
        for (int i = 0; i < 20; i++)
        {
            area.TextInput("x");
            sw.Restart();
            using var bmp = area.RenderToBitmap(width, height);
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine($"textarea keystroke frame: avg {times.Average():F1} ms, max {times.Max():F1} ms (text now {controller.Text.Length} chars)");
    }
    return;
}

Headless.Screenshot(new DemoRoot(page, dark, rtl), args[shot + 1], width, height, steps);

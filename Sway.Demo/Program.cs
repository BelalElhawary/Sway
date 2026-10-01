using Sway.Core.Platform;
using Sway.Core.Sample;

var app = App.Create().AddStylesheet("app.css");

// `--bench [frames]` measures frame cost on the stress page and prints per-phase timings.
int bench = Array.IndexOf(args, "--bench");
if (bench >= 0)
{
    int frames = bench + 1 < args.Length && int.TryParse(args[bench + 1], out var n) ? n : 60;
    bool gpu = args.Contains("--gpu"); // render on a real GPU surface instead of the CPU raster one
    // `--page effects|motion|forms|menu` benchmarks another page (the hover/scroll/click scenarios assume the stress layout).
    int pageArg = Array.IndexOf(args, "--page");
    string benchPage = pageArg >= 0 && pageArg + 1 < args.Length ? args[pageArg + 1] : "stress";
    switch (benchPage)
    {
        case "effects": app.Benchmark<EffectsDemo>(800, 620, frames, gpu); break;
        case "motion": app.Benchmark<MotionDemo>(800, 420, frames, gpu); break;
        case "forms": app.Benchmark<FormsDemo>(760, 640, frames, gpu); break;
        default: app.Benchmark<StressDemo>(1000, 700, frames, gpu); break;
    }
    return;
}

// `--screenshot out.png` renders headlessly, which is handy for checking layout and input.
// Scripted steps run in order before the capture:
//   --move x,y   --click x,y   --wheel x,y,notches   --key Name   --bench [frames] [--gpu] [--page name]   --verify   --css file   --advance ms   --type text   --key [ctrl+][shift+]Name   (and --page menu|counter|layout|layout2|interaction|forms)
int shot = Array.IndexOf(args, "--screenshot");
if (shot < 0 || shot + 1 >= args.Length)
{
    app.Run<MainWindow>("My App", 1024, 768);
    return;
}

var steps = new List<Action<UiHost>>();
string page = "menu";
for (int i = 0; i < args.Length; i++)
{
    string Next() => args[++i];
    float[] Numbers(string s) => s.Split(',').Select(float.Parse).ToArray();

    switch (args[i])
    {
        case "--page": page = Next(); break;
        case "--css": app.AddStylesheet(Path.GetFullPath(Next())); break;
        case "--move": { var n = Numbers(Next()); steps.Add(h => h.PointerMove(n[0], n[1])); break; }
        case "--click":
        {
            var n = Numbers(Next());
            steps.Add(h => { h.PointerMove(n[0], n[1]); h.PointerDown(n[0], n[1]); h.PointerUp(n[0], n[1]); });
            break;
        }
        case "--wheel": { var n = Numbers(Next()); steps.Add(h => h.Wheel(n[0], n[1], 0, n[2])); break; }
        case "--verify":
            steps.Add(h =>
            {
                string? layout = h.VerifyLayout();
                Console.WriteLine(layout is null ? "verify: layout matches a full relayout" : $"verify: LAYOUT MISMATCH {layout}");
                string? styles = h.VerifyStyles();
                Console.WriteLine(styles is null ? "verify: styles match a full restyle" : $"verify: STYLE MISMATCH {styles}");
            });
            break;
        case "--advance": { float ms = float.Parse(Next()); steps.Add(h => h.AdvanceClock(ms)); break; }
        case "--type": { string text = Next(); steps.Add(h => h.TextInput(text)); break; }
        case "--key":
        {
            // Modifiers can prefix the key: ctrl+a, shift+Tab, ctrl+shift+z.
            var parts = Next().Split('+');
            string key = parts[^1];
            var mods = parts[..^1].Select(m => m.ToLowerInvariant()).ToHashSet();
            steps.Add(h =>
            {
                h.Ctrl = mods.Contains("ctrl");
                h.Shift = mods.Contains("shift");
                h.KeyDown(key, key, false);
                h.KeyUp(key, key);
                h.Ctrl = h.Shift = false;
            });
            break;
        }
    }
}

string path = args[shot + 1];
switch (page)
{
    case "counter": app.Screenshot<CounterDemo>(path, 800, 600, steps); break;
    case "layout": app.Screenshot<LayoutDemo>(path, 800, 720, steps); break;
    case "layout2": app.Screenshot<LayoutDemo2>(path, 800, 720, steps); break;
    case "interaction": app.Screenshot<InteractionDemo>(path, 600, 560, steps); break;
    case "forms": app.Screenshot<FormsDemo>(path, 760, 640, steps); break;
    case "effects": app.Screenshot<EffectsDemo>(path, 800, 620, steps); break;
    case "motion": app.Screenshot<MotionDemo>(path, 800, 420, steps); break;
    case "stress": app.Screenshot<StressDemo>(path, 1000, 700, steps); break;
    default: app.Screenshot<MainWindow>(path, 1000, 700, steps); break;
}

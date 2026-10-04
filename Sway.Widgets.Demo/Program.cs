using SkiaSharp;
using Sway.Widgets;
using Sway.Widgets.Demo;

// `--screenshot out.png` renders headlessly; steps: --click x,y  --move x,y  --advance ms
int shot = Array.IndexOf(args, "--screenshot");
if (shot < 0 || shot + 1 >= args.Length)
{
    App.Run(new MainApp(), "Sway Widgets", 900, 700);
    return;
}

var steps = new List<Action<WidgetsBinding>>();
string page = "counter";
for (int i = 0; i < args.Length; i++)
{
    float[] Numbers(string s) => s.Split(',').Select(float.Parse).ToArray();
    switch (args[i])
    {
        case "--move": { var n = Numbers(args[++i]); steps.Add(b => b.Gestures.PointerMove(n[0], n[1])); break; }
        case "--click":
        {
            var n = Numbers(args[++i]);
            steps.Add(b => { b.Gestures.PointerMove(n[0], n[1]); b.Gestures.PointerDown(n[0], n[1]); b.Gestures.PointerUp(n[0], n[1]); });
            break;
        }
        case "--page": page = args[++i]; break;
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

App.Screenshot(new MainApp(page), args[shot + 1], 900, 700, steps);

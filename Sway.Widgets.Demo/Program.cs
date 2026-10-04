using SkiaSharp;
using Sway.Widgets;
using Sway.Widgets.Demo;

// `--screenshot out.png` renders headlessly; steps: --click x,y  --move x,y  --advance ms
int shot = Array.IndexOf(args, "--screenshot");
if (shot < 0 || shot + 1 >= args.Length)
{
    App.Run(new CounterApp(), "Sway Widgets", 800, 600);
    return;
}

var steps = new List<Action<WidgetsBinding>>();
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
        case "--advance": { float ms = float.Parse(args[++i]); steps.Add(b => b.AdvanceClock(TimeSpan.FromMilliseconds(ms))); break; }
    }
}

App.Screenshot(new CounterApp(), args[shot + 1], 800, 600, steps);

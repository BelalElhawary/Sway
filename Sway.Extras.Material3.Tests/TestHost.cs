using Sway.Widgets;
using Sway.Widgets.Tests;

namespace Sway.Extras.Material3.Tests;

/// <summary>A stateful host so a control can rebuild with its new value, like an app would, plus helpers shared by the component tests.</summary>
sealed class TestHost(Func<TestHost, Widget> build) : StatefulWidget
{
    public override State CreateState() => new TestHostState();
    internal Func<TestHost, Widget> Build => build;
    public Action? Refresh;

    public static Widget Top(Widget child) => new Align(Alignment.TopLeft, child);

    public static (Harness harness, Func<BuildContext> context) App(Widget body, int height = 300)
    {
        BuildContext? captured = null;
        var root = new MaterialApp(new Builder(ctx =>
        {
            captured = ctx;
            return body;
        }));
        return (new Harness(root, 400, height), () => captured!);
    }

    public static List<string> Texts(Harness h) => h.Find<RenderParagraph>().Select(p => p.PlainText).ToList();

    public static void Open(Harness h)
    {
        h.Pump();
        h.Advance(350);
        h.Pump();
    }

    public static Offset CenterOfText(Harness h, string text)
    {
        var p = h.Find<RenderParagraph>().First(r => r.PlainText == text);
        var o = p.LocalToGlobal(Offset.Zero);
        return new Offset(o.Dx + p.Size.Width / 2, o.Dy + p.Size.Height / 2);
    }

    public static void TapText(Harness h, string text)
    {
        var at = CenterOfText(h, text);
        h.Tap(at.Dx, at.Dy);
    }
}

sealed class TestHostState : State<TestHost>
{
    public override void InitState() => Widget.Refresh = () => SetState();
    public override Widget Build(BuildContext context) => Widget.Build(Widget);
}

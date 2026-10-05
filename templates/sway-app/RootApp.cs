using Sway.Extras.Material3;
using Sway.Widgets;

namespace SwayApp;

/// <summary>The app root: a Material 3 shell with a counter to get you started.</summary>
public class RootApp : StatefulWidget
{
    public override State CreateState() => new RootAppState();
}

class RootAppState : State<RootApp>
{
    int _count;

    public override Widget Build(BuildContext context) => new MaterialApp(
        themeMode: ThemeMode.System, theme: ThemeData.Light(), darkTheme: ThemeData.Dark(),
        home: new Scaffold(
            appBar: new AppBar(title: new Text("SwayApp")),
            body: new Center(new Column(mainAxisSize: MainAxisSize.Min, spacing: 16, children:
            [
                new Text($"You pressed the button {_count} times"),
                new FilledButton(new Text("Press me"), () => SetState(() => _count++)),
            ]))));
}

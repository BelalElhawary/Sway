using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class FormsDemo : StatefulWidget
{
    public override State CreateState() => new FormsDemoState();
}

class FormsDemoState : State<FormsDemo>
{
    readonly TextEditingController _name = new("Ada");
    readonly TextEditingController _notes = new("First line\nSecond line that is long enough to wrap around the edge of the box when it is narrow.");
    bool _agree = true, _notify;
    string _plan = "free";
    string? _country = "eg";
    string _submitted = "";

    public override Widget Build(BuildContext context) =>
        new SingleChildScrollView(padding: EdgeInsets.All(24), child: new Column(
            crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 16,
            children:
            [
                new Text("Forms", style: new TextStyle(FontSize: 24, FontWeight: FontWeight.Bold)),
                new SizedBox(width: 320, child: new TextField(_name,
                    decoration: new InputDecoration(LabelText: "Name", HintText: "Your name", HelperText: "Shown on your profile"),
                    onSubmitted: v => SetState(() => _submitted = v))),
                new SizedBox(width: 320, child: new TextField(
                    decoration: new InputDecoration(LabelText: "Password", HintText: "••••", Prefix: new Text("🔒")), obscureText: true)),
                new SizedBox(width: 320, child: new TextField(_notes, maxLines: 4, minLines: 3,
                    decoration: new InputDecoration(LabelText: "Notes"))),
                new Row(spacing: 8, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Checkbox(_agree, v => SetState(() => _agree = v)), new Text("I agree to the terms"),
                ]),
                new Row(spacing: 8, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Switch(_notify, v => SetState(() => _notify = v)), new Text(_notify ? "Notifications on" : "Notifications off"),
                ]),
                new Row(spacing: 16, mainAxisSize: MainAxisSize.Min, children:
                [
                    ..new[] { "free", "pro", "team" }.Select(p => (Widget)new Row(spacing: 6, mainAxisSize: MainAxisSize.Min, children:
                        [new Radio<string>(p, _plan, v => SetState(() => _plan = v)), new Text(p)])),
                ]),
                new SizedBox(width: 220, child: new DropdownButton<string>(
                    [new("eg", new Text("Egypt")), new("us", new Text("United States")), new("de", new Text("Germany")),
                     new("jp", new Text("Japan")), new("xx", new Text("Disabled option"), false)],
                    _country, v => SetState(() => _country = v), hint: new Text("Country"))),
                new Row(spacing: 8, mainAxisSize: MainAxisSize.Min, children:
                [
                    new Button(new Text("Submit"), () => SetState(() => _submitted = $"{_name.Text}/{_plan}/{_country}/agree={_agree}")),
                    new Button(new Text("Reset"), () => SetState(() => { _name.Text = ""; _agree = false; }), ButtonVariant.Outlined),
                    new Button(new Text("Disabled"), null),
                ]),
                new Text(_submitted, style: new TextStyle(Color: Colors.Green)),
            ]));
}

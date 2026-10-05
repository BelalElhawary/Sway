using SkiaSharp;
using Sway.Widgets;

namespace Sway.Widgets.Demo;

class FormsPage : StatefulWidget
{
    public override State CreateState() => new FormsPageState();
}

class FormsPageState : State<FormsPage>
{
    readonly TextEditingController _name = new("Ada");
    readonly TextEditingController _email = new("ada@");
    readonly TextEditingController _notes = new("First line\nSecond line that is long enough to wrap around the edge of the box when it is narrow.");
    bool _agree = true, _notify;
    string _plan = "free";
    string? _country = "eg";
    string _submitted = "";

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        bool emailBad = !_email.Text.Contains('@') || !_email.Text.Contains('.');

        return Ui.Page("Forms", [
            Ui.Section(context, "Text fields", new Wrap(spacing: 16, runSpacing: 20, children:
            [
                new SizedBox(width: 280, child: new TextField(_name,
                    decoration: new InputDecoration(LabelText: "Name", HintText: "Your name", HelperText: "Shown on your profile"),
                    onSubmitted: v => SetState(() => _submitted = v))),
                new SizedBox(width: 280, child: new TextField(_email,
                    decoration: new InputDecoration(LabelText: "Email", ErrorText: emailBad ? "Enter a valid address" : null, Prefix: new Icon(Icons.Info)),
                    onChanged: _ => SetState())),
                new SizedBox(width: 280, child: new TextField(decoration: new InputDecoration(LabelText: "Password", Filled: true, Suffix: new Icon(Icons.Check)), obscureText: true)),
                new SizedBox(width: 280, child: new TextField(enabled: false, decoration: new InputDecoration(LabelText: "Disabled", HintText: "Not editable"))),
                new SizedBox(width: 580, child: new TextField(_notes, maxLines: 4, minLines: 3, decoration: new InputDecoration(LabelText: "Notes"))),
            ])),

            Ui.Section(context, "Selection controls", new Wrap(spacing: 28, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new Row(mainAxisSize: MainAxisSize.Min, children: [new Checkbox(_agree, v => SetState(() => _agree = v)), new Text("I agree to the terms")]),
                new Row(mainAxisSize: MainAxisSize.Min, children: [new Switch(_notify, v => SetState(() => _notify = v)), new SizedBox(width: 8), new Text(_notify ? "Notifications on" : "Notifications off")]),
                ..new[] { "free", "pro", "team" }.Select(p => (Widget)new Row(mainAxisSize: MainAxisSize.Min, children:
                    [new Radio<string>(p, _plan, v => SetState(() => _plan = v)), new Text(p)])),
                new Row(mainAxisSize: MainAxisSize.Min, children: [new Checkbox(true, null), new Text("Disabled")]),
                new Switch(true, null),
            ])),

            Ui.Section(context, "Dropdown", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                new SizedBox(width: 240, child: new DropdownButton<string>(
                    [new("eg", new Text("Egypt")), new("us", new Text("United States")), new("de", new Text("Germany")),
                     new("jp", new Text("Japan")), new("xx", new Text("Disabled option"), false)],
                    _country, v => SetState(() => _country = v), label: "Country")),
                new SizedBox(width: 240, child: new DropdownButton<string>(
                    [new("a", new Text("Option A")), new("b", new Text("Option B"))], null, v => { }, hint: new Text("Pick one"))),
            ])),

            Ui.Section(context, "Submit", new Row(spacing: 12, children:
            [
                new FilledButton(new Text("Submit"), () => SetState(() => _submitted = $"{_name.Text} / {_plan} / {_country} / agree={_agree}")),
                new OutlinedButton(new Text("Reset"), () => SetState(() => { _name.Text = ""; _agree = false; })),
                new Text(_submitted, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: theme.ColorScheme.Primary))),
            ])),
        ]);
    }
}

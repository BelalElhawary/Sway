using Sway.Extras.Ibm;
using Sway.Widgets;

namespace Sway.Example;

// The rest of the Carbon components: form controls, feedback, layout, navigation and overlays.
partial class CarbonPageState
{
    string _radio = "b";
    bool _toggle = true, _smallToggle;
    double _number = 3, _slider = 40;
    string? _combo, _treeSelected = "src";
    IReadOnlyList<int> _multi = [1];
    DateTime? _date = new(2026, 10, 5);
    int _tab, _tabContained, _step = 1, _structured;
    bool _tileSelected;
    string _nav = "overview";
    IReadOnlyList<PickedFile> _files = [];
    readonly TextEditingController _notes = new();

    static readonly CarbonDropdownItem<string>[] Fruits =
        [new("apple", "Apple"), new("banana", "Banana"), new("blueberry", "Blueberry"), new("cherry", "Cherry"), new("grape", "Grape")];

    static Widget Spread(params Widget[] children) => new Wrap(spacing: 24, runSpacing: 24, crossAxisAlignment: WrapCrossAlignment.Start, children: children);

    Widget MoreSections(CarbonThemeData t)
    {
        var c = t.Colors;
        TextStyle body = t.Type.Body01.Merge(new TextStyle(Color: c.TextPrimary));
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            Section(t, "Radio, toggle and link", Spread(
                new CarbonRadioGroup<string>([new("a", "Alpha"), new("b", "Beta"), new("c", "Gamma", false)], _radio, v => SetState(() => _radio = v), legend: "Pick one"),
                new CarbonToggle(_toggle, v => SetState(() => _toggle = v), label: "Notifications"),
                new CarbonToggle(_smallToggle, v => SetState(() => _smallToggle = v), label: "Small", small: true),
                new CarbonLink("Read the docs", () => { }),
                new CarbonLink("Disabled link", null, disabled: true))),

            Section(t, "More inputs", Spread(
                new SizedBox(width: 280, child: new CarbonTextArea(_notes, label: "Notes", placeholder: "Write something", maxCount: 120, onLayer: true)),
                new SizedBox(width: 200, child: new CarbonNumberInput(_number, v => SetState(() => _number = v), 0, 10, 0.5, label: "Quantity", onLayer: true)),
                new SizedBox(width: 320, child: new CarbonSlider(_slider, v => SetState(() => _slider = v), label: "Volume")),
                new SizedBox(width: 280, child: new CarbonSelect<string>(Fruits, _combo, v => SetState(() => _combo = v), label: "Select", helperText: "A single choice", onLayer: true)),
                new SizedBox(width: 280, child: new CarbonComboBox<string>(Fruits, _combo, v => SetState(() => _combo = v), label: "Combo box", onLayer: true)),
                new SizedBox(width: 280, child: new CarbonMultiSelect<int>([new(1, "One"), new(2, "Two"), new(3, "Three")], _multi, v => SetState(() => _multi = v), label: "Multi-select", onLayer: true)),
                new SizedBox(width: 240, child: new CarbonDatePicker(_date, v => SetState(() => _date = v), label: "Date", onLayer: true)),
                new SizedBox(width: 320, child: new CarbonFileUploader(_files, f => SetState(() => _files = f), description: "Max file size is 500 MB.")))),

            Section(t, "Notifications and loading", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new CarbonNotification(CarbonNotificationKind.Info, "Heads up", "Maintenance starts at 22:00 UTC.", () => { }),
                new CarbonNotification(CarbonNotificationKind.Success, "Saved", "Your changes are live."),
                new CarbonNotification(CarbonNotificationKind.Warning, "Quota nearly reached", "You have used 90% of your plan.", actionLabel: "Upgrade", onAction: () => { }),
                new CarbonNotification(CarbonNotificationKind.Error, "Deploy failed", "The build exited with code 1.", () => { }),
                Spread(
                    new SizedBox(width: 300, child: new CarbonProgressBar(60, label: "Uploading", helperText: "6 of 10 MB")),
                    new SizedBox(width: 300, child: new CarbonProgressBar(null, label: "Working")),
                    new SizedBox(width: 300, child: new CarbonProgressBar(100, label: "Done", status: CarbonStatus.Finished)),
                    new CarbonLoading(), new CarbonLoading(small: true),
                    new CarbonInlineLoading("Saving..."), new CarbonInlineLoading("Saved", CarbonStatus.Finished)),
                new SizedBox(width: 360, child: new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, spacing: 12,
                    children: [new CarbonSkeleton(height: 24), new CarbonSkeletonText(3)])),
                new CarbonProgressIndicator([new("Account", "Done"), new("Plan", "Current"), new("Review"), new("Invalid", Invalid: true)], _step, i => SetState(() => _step = i)),
            ])),

            Section(t, "Layout", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 24, children:
            [
                new CarbonBreadcrumb([new("Home", () => { }), new("Components", () => { }), new("Breadcrumb")]),
                new CarbonAccordion([new("Section 1", new Text("The content of the first section.")), new("Section 2", new Text("The second.")), new("Disabled", new Text("-"), true)]),
                new CarbonTabs([new("Overview", new Padding(EdgeInsets.All(16), new Text("Overview tab"))), new("Usage", new Padding(EdgeInsets.All(16), new Text("Usage tab"))), new("Disabled", new Text("-"), true)],
                    _tab, i => SetState(() => _tab = i)),
                new CarbonTabs([new("One", new Container(color: c.Background, padding: EdgeInsets.All(16), child: new Text("Contained one"))),
                    new("Two", new Container(color: c.Background, padding: EdgeInsets.All(16), child: new Text("Contained two")))],
                    _tabContained, i => SetState(() => _tabContained = i), contained: true),
                Spread(
                    new CarbonTile(new Text("A plain tile"), width: 200, height: 96),
                    new CarbonClickableTile(new Text("Clickable tile"), () => { }, width: 200, height: 96),
                    new CarbonSelectableTile(new Text("Selectable tile"), _tileSelected, v => SetState(() => _tileSelected = v), width: 200, height: 96),
                    new SizedBox(width: 240, child: new CarbonExpandableTile(new Text("Expandable tile"), new Text("The part below the fold.")))),
                CarbonStructuredList.OfText(["Name", "Role", "Region"], [["Ada", "Engineer", "EU"], ["Grace", "Admiral", "US"], ["Linus", "Maintainer", "EU"]],
                    selectedIndex: _structured, onSelected: i => SetState(() => _structured = i)),
                Spread(
                    new SizedBox(width: 320, child: new CarbonContainedList("Contained list", [new("Dashboard", () => { }, Icons.Home), new("Settings", () => { }, Icons.Settings), new("Static row")])),
                    new SizedBox(width: 280, child: new CarbonTreeView([new("src", "src", [new("a", "App.cs"), new("b", "Page.cs")]), new("docs", "docs", [new("c", "Readme.md")])],
                        _treeSelected, n => SetState(() => _treeSelected = n.Id), ["src"]))),
                new CarbonCodeSnippet("dotnet add package Sway.Extras.Ibm"),
                new CarbonCodeSnippet("var app = new CarbonApp(\n    home: new Text(\"Hello\"));\nawait app.RunAsync();", CarbonCodeType.Multi),
                new Row(spacing: 8, children: [new Text("Install with", style: body), new CarbonCodeSnippet("dotnet build", CarbonCodeType.Inline)]),
            ])),

            Section(t, "Overlays and shell", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
            [
                Spread(
                    new CarbonTooltip("Save your work", new CarbonButton(new Text("Hover me"), () => { }, CarbonButtonKind.Tertiary, CarbonButtonSize.Medium)),
                    new CarbonPopover(new CarbonButton(new Text("Popover"), null, CarbonButtonKind.Tertiary, CarbonButtonSize.Medium), new Text("Anything can go in a popover.")),
                    new Row(spacing: 4, children: [new Text("Toggletip", style: body), new CarbonToggletip(new Text("Short helpful context."))]),
                    new CarbonOverflowMenu([new("Edit", () => { }, Icon: Icons.Edit), new("Share", () => { }), new("Delete", () => { }, Danger: true, DividerBefore: true, Icon: Icons.Delete)]),
                    new Builder(ctx => new CarbonButton(new Text("Open modal"), () => CarbonModal.Show(ctx, "Delete server?", new Text("This permanently removes the server and its data."),
                        "Servers", "Delete", () => true, "Cancel", danger: true), CarbonButtonKind.Danger, CarbonButtonSize.Medium)),
                    new Builder(ctx => new CarbonButton(new Text("Show toast"), () => CarbonToast.Show(ctx, CarbonNotificationKind.Success, "Saved", "All changes were saved."),
                        CarbonButtonKind.Primary, CarbonButtonSize.Medium))),
                new SizedBox(height: 280, child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                [
                    new CarbonHeader("Platform", "IBM Cloud", () => { }, [new(Icons.Search, () => { }), new(Icons.Notifications, () => { }), new(Icons.Person, () => { })]),
                    new Expanded(new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
                    [
                        new CarbonSideNav([new("overview", "Overview", Icons.Home), new("cat", "Resources", Icons.Folder, [new("vm", "Virtual servers"), new("net", "Networks")]), new("settings", "Settings", Icons.Settings)],
                            _nav, n => SetState(() => _nav = n.Id), ["cat"], width: 240),
                        new Expanded(new Container(color: c.Background, padding: EdgeInsets.All(16), child: new Text($"Selected: {_nav}", style: body))),
                    ])),
                ])),
            ])),
        ]);
    }
}

using SkiaSharp;
using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

/// <summary>One of every Material 3 component, grouped the way the Material guidelines group them.</summary>
class MaterialPage : StatefulWidget
{
    public override State CreateState() => new MaterialPageState();
}

class MaterialPageState : State<MaterialPage>
{
    static readonly string[] Weekdays = ["Mon", "Tue", "Wed"];

    readonly TextEditingController _search = new();
    readonly HashSet<string> _segments = new() { "Day" };
    readonly bool[] _panelOpen = [true, false];

    int _count;
    readonly HashSet<string> _chips = new() { "Filter" };
    bool _deletableShown = true;
    bool _check = true, _switch = true;
    string _radio = "b";
    float _slider = 0.4f;
    RangeValues _range = new(20, 70);
    int _tab, _bar, _rail, _step;
    string? _dropdown = "b";
    string _picked = "Nothing picked yet";
    int? _sortColumn;
    bool _sortAscending = true;
    readonly HashSet<string> _selectedRows = new();

    static readonly (string Name, int Calories)[] Desserts = [("Frozen yogurt", 159), ("Ice cream sandwich", 237), ("Eclair", 262), ("Cupcake", 305)];

    IEnumerable<(string Name, int Calories)> SortedDesserts()
    {
        var rows = Desserts.AsEnumerable();
        if (_sortColumn == 0) rows = rows.OrderBy(d => d.Name);
        if (_sortColumn == 1) rows = rows.OrderBy(d => d.Calories);
        return _sortAscending ? rows : rows.Reverse();
    }

    static NavigationDestination[] Destinations() =>
    [
        new(Icons.Home, "Home"), new(Icons.Favorite, "Saved"), new(Icons.Info, "About"),
    ];

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;

        return Ui.Page("Material 3 components", [
            Ui.Section(context, "Buttons", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Wrap(spacing: 12, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new FilledButton(new Text("Filled"), () => SetState(() => _count++)),
                    new FilledTonalButton(new Text("Tonal"), () => SetState(() => _count++)),
                    new ElevatedButton(new Text("Elevated"), () => SetState(() => _count++)),
                    new OutlinedButton(new Text("Outlined"), () => SetState(() => _count++)),
                    new TextButton(new Text("Text"), () => SetState(() => _count++)),
                    new FilledButton(new Text("With icon"), () => SetState(() => _count++), Icons.Add),
                    new FilledButton(new Text("Disabled"), null),
                    new OutlinedButton(new Text("Disabled"), null),
                ]),
                new Wrap(spacing: 8, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    new IconButton(new Icon(Icons.Favorite), () => { }),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Filled),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Tonal),
                    new IconButton(new Icon(Icons.Favorite), () => { }, IconButtonVariant.Outlined),
                    new IconButton(new Icon(Icons.Delete), null),
                    new Text($"  pressed {_count} times", style: theme.TextTheme.BodyMedium),
                ]),
            ])),

            Ui.Section(context, "Chips", new Wrap(spacing: 8, runSpacing: 8, children:
            [
                ..new[] { "Filter", "Search", "Music", "Travel" }.Select(c => (Widget)new Chip(new Text(c),
                    () => SetState(() => { if (!_chips.Remove(c)) _chips.Add(c); }), selected: _chips.Contains(c))),
                new Chip(new Text("Assist"), () => { }, icon: Icons.Info),
                ..(_deletableShown ? [new Chip(new Text("Deletable"), () => { }, onDeleted: () => SetState(() => _deletableShown = false))] : Array.Empty<Widget>()),
            ])),

            Ui.Section(context, "Cards and lists", new Wrap(spacing: 12, runSpacing: 12, children:
            [
                ..new[] { CardVariant.Elevated, CardVariant.Filled, CardVariant.Outlined }.Select(v => (Widget)new SizedBox(width: 250, child:
                    new Card(variant: v, onTap: () => Dialogs.ShowSnackBar(context, $"{v} card tapped"), child: new Padding(EdgeInsets.All(16), new Column(
                        crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
                        [
                            new Text(v.ToString(), style: theme.TextTheme.TitleMedium),
                            new Text("Tap me for a snack bar.", style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnSurfaceVariant))),
                        ]))))),
                new SizedBox(width: 340, child: new Card(variant: CardVariant.Outlined, child: new Column(mainAxisSize: MainAxisSize.Min, children:
                [
                    new ListTile(new Text("Inbox"), new Text("12 unread"), new Icon(Icons.Home), new Text("12"), () => { }, selected: true),
                    new Divider(height: 1),
                    new ListTile(new Text("Favourites"), null, new Icon(Icons.Favorite), null, () => { }),
                    new Divider(height: 1),
                    new ListTile(new Text("Settings"), new Text("Account and privacy"), new Icon(Icons.Info), new Icon(Icons.ChevronRight), () => { }),
                ]))),
            ])),

            Ui.Section(context, "Selection controls", new Wrap(spacing: 28, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new Row(mainAxisSize: MainAxisSize.Min, children: [new Checkbox(_check, v => SetState(() => _check = v)), new Text("Checkbox")]),
                new Row(mainAxisSize: MainAxisSize.Min, children: [new Switch(_switch, v => SetState(() => _switch = v)), new SizedBox(width: 8), new Text("Switch")]),
                ..new[] { "a", "b", "c" }.Select(v => (Widget)new Row(mainAxisSize: MainAxisSize.Min, children:
                    [new Radio<string>(v, _radio, x => SetState(() => _radio = x)), new Text($"Radio {v}")])),
            ])),

            Ui.Section(context, "Sliders", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                new SizedBox(width: 360, child: new Slider(_slider, v => SetState(() => _slider = v), label: v => $"{v:P0}")),
                new SizedBox(width: 360, child: new Slider(_slider, v => SetState(() => _slider = v), divisions: 10)),
                new SizedBox(width: 360, child: new RangeSlider(_range, v => SetState(() => _range = v), 0, 100, label: v => $"{v:0}")),
                new SizedBox(width: 360, child: new RangeSlider(_range, v => SetState(() => _range = v), 0, 100, divisions: 10)),
                new Text($"Slider {_slider:P0}, range {_range.Start:0} to {_range.End:0}", style: theme.TextTheme.BodySmall),
            ])),

            Ui.Section(context, "Segmented button, chips and badge", new Wrap(spacing: 24, runSpacing: 16, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new SegmentedButton<string>(
                    [new("Day", "Day"), new("Week", "Week"), new("Month", "Month")], _segments,
                    sel => SetState(() => { _segments.Clear(); foreach (var v in sel) _segments.Add(v); })),
                new Badge(new Icon(Icons.Favorite, 28), "3"),
                new Badge(new Icon(Icons.Info, 28)),
                new Tooltip("A tooltip on hover", new FilledTonalButton(new Text("Hover me"), () => { })),
            ])),

            Ui.Section(context, "Text fields and search", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                new SizedBox(width: 280, child: new TextField(decoration: new InputDecoration(LabelText: "Outlined", HelperText: "Helper text"))),
                new SizedBox(width: 280, child: new TextField(decoration: new InputDecoration(LabelText: "Filled", Filled: true))),
                new SizedBox(width: 280, child: new TextField(decoration: new InputDecoration(LabelText: "Error", ErrorText: "Something is wrong"))),
                new SizedBox(width: 280, child: new SearchBar(_search, "Search components")),
            ])),

            Ui.Section(context, "Dropdown and menu", new Wrap(spacing: 16, runSpacing: 16, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new SizedBox(width: 240, child: new DropdownButton<string>(
                    [new("a", new Text("Option A")), new("b", new Text("Option B")), new("c", new Text("Option C"))],
                    _dropdown, v => SetState(() => _dropdown = v), label: "Dropdown")),
                new PopupMenuButton<string>(
                    [new MenuItem<string>("copy", new Text("Copy"), Icon: Icons.Edit), new MenuItem<string>("paste", new Text("Paste"), Enabled: false),
                     new MenuDivider<string>(), new MenuItem<string>("delete", new Text("Delete"), Icon: Icons.Delete)],
                    v => SetState(() => _picked = $"Menu: {v}")),
            ])),

            Ui.Section(context, "Tabs", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children:
            [
                new TabBar([new Tab("Flights", Icons.Home), new Tab("Trips", Icons.Favorite), new Tab("Explore", Icons.Info)], _tab, i => SetState(() => _tab = i)),
                new Padding(EdgeInsets.All(16), new TabBarView(_tab,
                [
                    new Text("Flights tab"), new Text("Trips tab"), new Text("Explore tab"),
                ])),
            ])),

            Ui.Section(context, "Navigation", new Wrap(spacing: 16, runSpacing: 16, children:
            [
                new SizedBox(width: 360, child: new NavigationBar(_bar, Destinations(), i => SetState(() => _bar = i))),
                new SizedBox(width: 80, height: 260, child: new NavigationRail(_rail, Destinations(), i => SetState(() => _rail = i))),
                new SizedBox(width: 280, height: 260, child: new NavigationDrawer(_rail, Destinations(), i => SetState(() => _rail = i), new Text("Mail"))),
            ])),

            Ui.Section(context, "App bar, FAB and scaffold", new SizedBox(height: 260, child: new ClipRRect(BorderRadius.Circular(12),
                new Scaffold(
                    body: new Center(new Text("Scaffold body", style: theme.TextTheme.BodyLarge)),
                    appBar: new AppBar(new Text("App bar"), leading: new IconButton(new Icon(Icons.Menu), () => { }),
                        actions: [new IconButton(new Icon(Icons.Favorite), () => { })]),
                    floatingActionButton: new FloatingActionButton(new Icon(Icons.Add), () => { }))))),

            Ui.Section(context, "Sheets and pickers", new Wrap(spacing: 12, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new FilledTonalButton(new Text("Bottom sheet"), () => Panels.ShowBottomSheet(context, (_, close) => new BottomSheet(new Padding(EdgeInsets.All(24),
                    new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, spacing: 12, children:
                    [new Text("Bottom sheet", style: theme.TextTheme.TitleMedium), new FilledButton(new Text("Close"), close)]))))),
                new FilledTonalButton(new Text("Side drawer"), () => Panels.ShowDrawer(context, (_, close) =>
                    new NavigationDrawer(0, Destinations(), _ => close(), new Text("Drawer")))),
                new FilledTonalButton(new Text("Date picker"), () => DatePicker.Show(context, DateTime.Today, new DateTime(2020, 1, 1), new DateTime(2035, 12, 31),
                    d => SetState(() => _picked = $"Date: {d:yyyy-MM-dd}"))),
                new FilledTonalButton(new Text("Date range"), () => DateRangePicker.Show(context, new DateTime(2020, 1, 1), new DateTime(2035, 12, 31),
                    r => SetState(() => _picked = $"Range: {r.Start:yyyy-MM-dd} to {r.End:yyyy-MM-dd}"))),
                new FilledTonalButton(new Text("Time picker"), () => TimePicker.Show(context, new TimeOfDay(9, 30), t => SetState(() => _picked = $"Time: {t}"))),
                new Text(_picked, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.Primary))),
            ])),

            Ui.Section(context, "Expansion", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
            [
                new ExpansionTile(new Text("Expansion tile"), [new ListTile(new Text("First child")), new ListTile(new Text("Second child"))],
                    new Text("Tap to expand"), new Icon(Icons.Info)),
                new ExpansionPanelList(
                [
                    new ExpansionPanel(new Text("Panel one"), new Padding(EdgeInsets.All(16), new Text("Body of panel one")), _panelOpen[0]),
                    new ExpansionPanel(new Text("Panel two"), new Padding(EdgeInsets.All(16), new Text("Body of panel two")), _panelOpen[1]),
                ], (i, open) => SetState(() => _panelOpen[i] = open)),
            ])),

            Ui.Section(context, "Stepper", new Wrap(spacing: 24, runSpacing: 16, children:
            [
                new SizedBox(width: 380, child: new Stepper(StepList(), _step, i => SetState(() => _step = i),
                    () => SetState(() => _step = Math.Min(_step + 1, 2)), () => SetState(() => _step = Math.Max(_step - 1, 0)))),
                new SizedBox(width: 520, child: new Stepper(StepList(), _step, i => SetState(() => _step = i),
                    () => SetState(() => _step = Math.Min(_step + 1, 2)), () => SetState(() => _step = Math.Max(_step - 1, 0)), StepperType.Horizontal)),
            ])),

            Ui.Section(context, "Data table", new DataTable(
                [
                    new DataColumn(new Text("Dessert"), Width: 200, OnSort: (i, asc) => SetState(() => { _sortColumn = i; _sortAscending = asc; })),
                    new DataColumn(new Text("Calories"), Numeric: true, Width: 140, OnSort: (i, asc) => SetState(() => { _sortColumn = i; _sortAscending = asc; })),
                ],
                SortedDesserts().Select(d => new DataRow([new DataCell(d.Name), new DataCell(d.Calories.ToString())], _selectedRows.Contains(d.Name),
                    v => SetState(() => { if (v) _selectedRows.Add(d.Name); else _selectedRows.Remove(d.Name); }))).ToList(),
                _sortColumn, _sortAscending,
                v => SetState(() => { _selectedRows.Clear(); if (v) foreach (var d in Desserts) _selectedRows.Add(d.Name); }))),

            Ui.Section(context, "Dialogs and snack bars", new Wrap(spacing: 12, runSpacing: 12, children:
            [
                new FilledButton(new Text("Show dialog"), () => Dialogs.Show(context, (ctx, close) => new AlertDialog(
                    icon: Icons.Info, title: new Text("Discard draft?"),
                    content: new Text("Your changes will be lost if you leave now."),
                    actions: [new TextButton(new Text("Cancel"), close), new TextButton(new Text("Discard"), () => { close(); Dialogs.ShowSnackBar(context, "Draft discarded", "Undo"); })]))),
                new OutlinedButton(new Text("Show snack bar"), () => Dialogs.ShowSnackBar(context, "Saved to your device", "Undo", () => { })),
            ])),

            Ui.Section(context, "Progress and dividers", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new LinearProgressIndicator(_slider),
                new Divider(),
                new Row(spacing: 16, children: [new CircularProgressIndicator(_slider), new CircularProgressIndicator()]),
            ])),

            Ui.Section(context, "Type scale", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 4, children:
            [
                new Text("Display Small", style: theme.TextTheme.DisplaySmall),
                new Text("Headline Medium", style: theme.TextTheme.HeadlineMedium),
                new Text("Title Large", style: theme.TextTheme.TitleLarge),
                new Text("Body Large: the quick brown fox jumps over the lazy dog", style: theme.TextTheme.BodyLarge),
                new Text("Label Large", style: theme.TextTheme.LabelLarge),
            ])),

            Ui.Section(context, "Colour roles", new Wrap(spacing: 8, runSpacing: 8, children:
            [
                Swatch("primary", s.Primary, s.OnPrimary), Swatch("primaryContainer", s.PrimaryContainer, s.OnPrimaryContainer),
                Swatch("secondaryContainer", s.SecondaryContainer, s.OnSecondaryContainer), Swatch("tertiaryContainer", s.TertiaryContainer, s.OnTertiaryContainer),
                Swatch("error", s.Error, s.OnError), Swatch("surfaceContainer", s.SurfaceContainer, s.OnSurface),
                Swatch("surfaceContainerHigh", s.SurfaceContainerHigh, s.OnSurface), Swatch("inverseSurface", s.InverseSurface, s.OnInverseSurface),
            ])),
        ]);
    }

    static Widget Swatch(string name, SKColor bg, SKColor fg) => new Container(width: 150, height: 56, alignment: Alignment.Center, color: bg,
        child: new Text(name, style: new TextStyle(Color: fg, FontSize: 12, FontWeight: FontWeight.W500)));

    List<Step> StepList() =>
    [
        new(new Text("Account"), new Text("Choose a username and password.")),
        new(new Text("Address"), new Text("Where should we send it?")),
        new(new Text("Confirm"), new Text("Review your details and finish.")),
    ];
}

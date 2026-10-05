using Sway.Extras.Ibm;
using Sway.Extras.Material3;
using Sway.Widgets;

namespace Sway.Example;

sealed record Server(int Id, string Name, string Region, string Status, int Cpu, DateTime Updated);

/// <summary>
/// The IBM Carbon components from Sway.Extras.Ibm. The rest of the demo is Material 3; this page hosts its own
/// CarbonApp, which follows the light/dark switch in the app bar.
/// </summary>
class CarbonPage : StatefulWidget
{
    public override State CreateState() => new CarbonPageState();
}

partial class CarbonPageState : State<CarbonPage>
{
    static readonly string[] Regions = ["us-south", "eu-de", "eu-gb", "jp-tok", "au-syd"];
    static readonly string[] States = ["Running", "Stopped", "Degraded", "Provisioning"];

    static readonly List<Server> Data = Enumerable.Range(1, 240).Select(i => new Server(
        i, $"server-{i:000}", Regions[i * 7 % Regions.Length], States[i * 3 % States.Length],
        i * 37 % 100, new DateTime(2026, 9, 1).AddHours(i * 13))).ToList();

    static readonly CarbonButtonSize[] Sizes = [CarbonButtonSize.Small, CarbonButtonSize.Medium, CarbonButtonSize.Large, CarbonButtonSize.ExtraLarge];
    static readonly string[] SizeLabels = ["Small", "Medium", "Large", "Extra large"];

    bool _gray;
    int _size = 2;
    bool _agree = true, _partial;
    int _country = 0, _clicks;
    string _tapped = "none";
    int _selected;
    readonly TextEditingController _search = new();

    static CarbonTagColor StatusColor(string s) => s switch
    {
        "Running" => CarbonTagColor.Green, "Stopped" => CarbonTagColor.Gray, "Degraded" => CarbonTagColor.Red, _ => CarbonTagColor.Blue,
    };

    public override Widget Build(BuildContext context)
    {
        bool dark = Theme.Of(context).Brightness == Brightness.Dark;
        return new CarbonApp(
            theme: _gray ? CarbonThemeData.Gray10() : CarbonThemeData.White(),
            darkTheme: _gray ? CarbonThemeData.Gray90() : CarbonThemeData.Gray100(),
            themeMode: dark ? CarbonThemeMode.Dark : CarbonThemeMode.Light,
            home: new LayoutBuilder((ctx, box) => new SingleChildScrollView(padding: EdgeInsets.All(box.MaxWidth < 600 ? 12 : 24),
                child: Content(ctx))));
    }

    static Widget Section(CarbonThemeData t, string title, Widget body) => new Container(color: t.Colors.Layer01, padding: EdgeInsets.All(16),
        child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            new Text(title, style: t.Type.Heading02.Merge(new TextStyle(Color: t.Colors.TextPrimary))),
            body,
        ]));

    Widget Content(BuildContext context)
    {
        var t = CarbonTheme.Of(context);
        var size = Sizes[_size];
        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
        [
            new Text("IBM Carbon", style: t.Type.Heading05.Merge(new TextStyle(Color: t.Colors.TextPrimary))),
            new Text("Sway.Extras.Ibm: its own theme, type scale and widgets. Nothing here comes from Material 3.",
                style: t.Type.Body01.Merge(new TextStyle(Color: t.Colors.TextSecondary))),
            new Align(Alignment.CenterLeft, new ContentSwitcher(["Standard", "Gray"], _gray ? 1 : 0, i => SetState(() => _gray = i == 1))),

            Section(t, "Buttons", new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 16, children:
            [
                new Align(Alignment.CenterLeft, new ContentSwitcher(SizeLabels, _size, i => SetState(() => _size = i))),
                new Wrap(spacing: 16, runSpacing: 16, children:
                [
                    new CarbonButton(new Text("Primary"), () => SetState(() => _clicks++), CarbonButtonKind.Primary, size),
                    new CarbonButton(new Text("Secondary"), () => SetState(() => _clicks++), CarbonButtonKind.Secondary, size),
                    new CarbonButton(new Text("Tertiary"), () => SetState(() => _clicks++), CarbonButtonKind.Tertiary, size),
                    new CarbonButton(new Text("Ghost"), () => SetState(() => _clicks++), CarbonButtonKind.Ghost, size),
                    new CarbonButton(new Text("Danger"), () => SetState(() => _clicks++), CarbonButtonKind.Danger, size),
                    new CarbonButton(new Text("Add"), () => SetState(() => _clicks++), CarbonButtonKind.Primary, size, Icons.Add),
                    new CarbonButton(new Text("Disabled"), null, CarbonButtonKind.Primary, size),
                    new CarbonButton(new Text("Disabled"), null, CarbonButtonKind.Tertiary, size),
                    new CarbonIconButton(Icons.Favorite, () => SetState(() => _clicks++), size),
                ]),
                new Text($"Pressed {_clicks} times", style: t.Type.HelperText01.Merge(new TextStyle(Color: t.Colors.TextSecondary))),
            ])),

            Section(t, "Selection", new Wrap(spacing: 32, runSpacing: 16, crossAxisAlignment: WrapCrossAlignment.Center, children:
            [
                new CarbonCheckbox(_agree, v => SetState(() => _agree = v), new Text("I agree to the terms")),
                new CarbonCheckbox(_partial, v => SetState(() => _partial = v), new Text("Some selected"), indeterminate: !_partial),
                new CarbonCheckbox(true, null, new Text("Disabled")),
            ])),

            Section(t, "Inputs", new Wrap(spacing: 16, runSpacing: 24, children:
            [
                new SizedBox(width: 280, child: new CarbonTextInput(label: "Name", placeholder: "Ada Lovelace", helperText: "Shown on your profile", onLayer: true)),
                new SizedBox(width: 280, child: new CarbonTextInput(label: "Email", placeholder: "you@example.com", errorText: "Enter a valid address", onLayer: true)),
                new SizedBox(width: 280, child: new CarbonDropdown<int>(
                    [new(0, "Egypt"), new(1, "Germany"), new(2, "Japan"), new(3, "Australia", false)], _country, v => SetState(() => _country = v),
                    label: "Country", onLayer: true)),
                new SizedBox(width: 280, child: new CarbonSearch(placeholder: "Search components", onLayer: true)),
            ])),

            Section(t, "Tags", new Wrap(spacing: 8, runSpacing: 8, children:
            [
                ..Enum.GetValues<CarbonTagColor>().Select(c => (Widget)new CarbonTag(c.ToString(), c)),
                new CarbonTag("Dismissible", CarbonTagColor.Blue, () => { }),
                new CarbonTag("Compact", CarbonTagColor.Purple, compact: true),
            ])),

            Section(t, "Data table", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new DataTable<Server>(
                    columns:
                    [
                        DataColumn<Server>.By("Name", r => r.Name, width: 160, onEdit: (r, text) => SetState(() => { int i = Data.IndexOf(r); if (i >= 0) Data[i] = r with { Name = text }; })),
                        new DataColumn<Server>("Status", r => r.Status, Width: 140, Cell: r => new CarbonTag(r.Status, StatusColor(r.Status), compact: size <= CarbonButtonSize.Medium)),
                        new DataColumn<Server>("Region", r => r.Region),
                        DataColumn<Server>.By("CPU", r => r.Cpu, v => $"{v}%", width: 90, align: TextAlign.End),
                        DataColumn<Server>.By("Updated", r => r.Updated, v => v.ToString("yyyy-MM-dd HH:mm")),
                    ],
                    rows: Data, title: "Servers", description: "Your virtual servers across all regions. The button size above sets the row height.",
                    size: size switch { CarbonButtonSize.Small => TableSize.Small, CarbonButtonSize.Medium => TableSize.Medium, CarbonButtonSize.Large => TableSize.Large, _ => TableSize.ExtraLarge },
                    selectable: true, searchable: true, pageSize: 10, maxBodyHeight: 480,
                    batchActions: [new BatchAction<Server>("Delete", rows => _tapped = $"delete {rows.Count}", Icons.Delete)],
                    onSelectionChanged: rows => SetState(() => _selected = rows.Count),
                    rowDetail: r => new Text($"{r.Name} runs in {r.Region} and was last updated {r.Updated:yyyy-MM-dd HH:mm}.",
                        style: t.Type.Body01.Merge(new TextStyle(Color: t.Colors.TextPrimary))),
                    onRowTap: r => SetState(() => _tapped = r.Name)),
                new Text($"{_selected} selected, last action: {_tapped}", style: t.Type.HelperText01.Merge(new TextStyle(Color: t.Colors.TextSecondary))),
            ])),

            Section(t, "Wide table", new DataTable<Server>(
                columns: [..Enumerable.Range(1, 6).Select(i => (DataColumn<Server>)new($"Column {i}", r => $"{r.Name} / {i}", Width: 200))],
                rows: Data.Take(40).ToList(), description: "Columns that do not fit scroll sideways; the header scrolls with them. Drag a header edge to resize, and the first column stays put.",
                size: TableSize.Small, maxBodyHeight: 200, resizable: true, stickyColumns: 1)),

            MoreSections(t),
        ]);
    }
}

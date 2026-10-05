using Sway.Extras.Ibm;
using Sway.Widgets;

namespace Sway.Example;

sealed record Server(int Id, string Name, string Region, string Status, int Cpu, DateTime Updated);

/// <summary>The Carbon data table from Sway.Extras.Ibm: search, sorting, selection, batch actions and pagination.</summary>
class DataPage : StatefulWidget
{
    public override State CreateState() => new DataPageState();
}

class DataPageState : State<DataPage>
{
    static readonly string[] Regions = ["us-south", "eu-de", "eu-gb", "jp-tok", "au-syd"];
    static readonly string[] States = ["Running", "Stopped", "Degraded", "Provisioning"];

    static readonly List<Server> Data = Enumerable.Range(1, 240).Select(i => new Server(
        i, $"server-{i:000}", Regions[i * 7 % Regions.Length], States[i * 3 % States.Length],
        i * 37 % 100, new DateTime(2026, 9, 1).AddHours(i * 13))).ToList();

    TableSize _size = TableSize.Large;
    int _selected;
    string _tapped = "none";

    static TagColor StatusColor(string s) => s switch
    {
        "Running" => TagColor.Green, "Stopped" => TagColor.Gray, "Degraded" => TagColor.Red, _ => TagColor.Blue,
    };

    public override Widget Build(BuildContext context)
    {
        var theme = Theme.Of(context);
        return Ui.Page("Data table", [
            Ui.Section(context, "Servers", new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 12, children:
            [
                new Wrap(spacing: 8, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.Center, children:
                [
                    ..new[] { TableSize.ExtraSmall, TableSize.Small, TableSize.Medium, TableSize.Large, TableSize.ExtraLarge }
                        .Select(z => (Widget)new Chip(new Text(z.ToString()), () => SetState(() => _size = z), selected: z == _size)),
                ]),
                new DataTable<Server>(
                    columns:
                    [
                        DataColumn<Server>.By("Name", r => r.Name, width: 160),
                        new DataColumn<Server>("Status", r => r.Status, Width: 140, Cell: r => new Tag(r.Status, StatusColor(r.Status), compact: _size <= TableSize.Small)),
                        new DataColumn<Server>("Region", r => r.Region),
                        DataColumn<Server>.By("CPU", r => r.Cpu, v => $"{v}%", width: 90, align: TextAlign.End),
                        DataColumn<Server>.By("Updated", r => r.Updated, v => v.ToString("yyyy-MM-dd HH:mm")),
                    ],
                    rows: Data, title: "Servers", description: "Your virtual servers across all regions.",
                    size: _size, selectable: true, searchable: true, pageSize: 10, maxBodyHeight: 480,
                    batchActions: [new BatchAction<Server>("Delete", rows => Dialogs.ShowSnackBar(context, $"Delete {rows.Count} servers?"), Icons.Delete)],
                    onSelectionChanged: rows => SetState(() => _selected = rows.Count),
                    rowDetail: r => new Text($"{r.Name} runs in {r.Region} and was last updated {r.Updated:yyyy-MM-dd HH:mm}. Click the arrow again to collapse.",
                        style: theme.TextTheme.BodyMedium),
                    onRowTap: r => SetState(() => _tapped = r.Name)),
                new Text($"{_selected} selected, last row tapped: {_tapped}", style: theme.TextTheme.BodySmall),
            ])),
            Ui.Section(context, "Wide table", new DataTable<Server>(
                columns: [..Enumerable.Range(1, 6).Select(i => (DataColumn<Server>)new($"Column {i}", r => $"{r.Name} / {i}", Width: 200))],
                rows: Data.Take(40).ToList(), description: "Columns that do not fit scroll sideways; the header scrolls with them.",
                size: TableSize.Small, maxBodyHeight: 200)),
        ]);
    }
}

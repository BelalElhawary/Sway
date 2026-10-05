---
sidebar_position: 5.5
---

# IBM Carbon

`Sway.Extras.Ibm` is [Carbon](https://carbondesignsystem.com), IBM's design system, as an optional package. It is
self-contained: its own theme tokens, type scale and widgets, built on the neutral parts of `Sway.Widgets`
(`Interactive`, `EditableText`, `Icon`). It does not use Material 3, and Material 3 does not know about it. You can
mix them in one app by nesting a `CarbonApp` inside a `MaterialApp`, which is what the example's Carbon page does.

```csharp
using Sway.Extras.Ibm;

App.Run(new CarbonApp(
    home: new MyHome(),
    theme: CarbonThemeData.White(),
    darkTheme: CarbonThemeData.Gray100()), "App", 900, 700);
```

## Theme

`CarbonThemeData` is a `CarbonColors` (Carbon's own token names: `Background`, `Layer01`, `Field01`, `BorderStrong01`,
`TextPrimary`, `LinkPrimary`, `ButtonPrimary`, `SupportError`, and so on) plus a `CarbonType` (`BodyCompact01`,
`Heading03`, `Label01`, `Code01`, ...). The four themes are `White`, `Gray10`, `Gray90` and `Gray100`; the first two
are light, the last two dark. `CarbonApp` follows the operating system by default (`themeMode: CarbonThemeMode.System`)
or you can force `Light` or `Dark`. Read the theme in your own widgets with `CarbonTheme.Of(context)`.

Carbon layers stack: `Layer01` sits on the page, `Layer02` on `Layer01`. A field on a layer uses the next field token,
so the inputs take `onLayer: true` when they sit on a `Layer01` tile.

IBM Plex is embedded in the package (SIL Open Font License), so the text looks the same everywhere.

## Controls

| Widget | Notes |
| --- | --- |
| `CarbonButton` | Primary, secondary, tertiary, ghost, danger (and tertiary and ghost danger) kinds; small to extra large; optional trailing icon; `GhostOnColor` for coloured bars. A null `onPressed` disables it. |
| `CarbonIconButton` | A square ghost button with just an icon. |
| `CarbonCheckbox` | 16px square; `indeterminate` shows a dash. |
| `CarbonTextInput` | Label, placeholder, helper and error text, three heights. Built on the core `EditableText`. |
| `CarbonSearch` | Magnifier and a clear button. |
| `CarbonDropdown<T>` | Opens a menu under the field; arrow keys, Enter and Escape work. |
| `ContentSwitcher` | Equal-width segments, one selected. |
| `CarbonTag` | Carbon colours, outline, close button and a `compact` 18px size. |
| `Pagination` | Items per page, range, previous and next; drops its wordier parts when narrow. |

Focus is Carbon's 2px inside border, not Material's halo.

## Data tables

`DataTable<T>` is the Carbon table, built from the controls above:

```csharp
new DataTable<Server>(
    columns:
    [
        DataColumn<Server>.By("Name", r => r.Name, width: 160),
        new DataColumn<Server>("Status", r => r.Status, Width: 140, Cell: r => new CarbonTag(r.Status, CarbonTagColor.Green)),
        DataColumn<Server>.By("CPU", r => r.Cpu, v => $"{v}%", align: TextAlign.End),
    ],
    rows: servers, title: "Servers", size: TableSize.Medium,
    selectable: true, searchable: true, pageSize: 10, maxBodyHeight: 480,
    batchActions: [new BatchAction<Server>("Delete", rows => Remove(rows), Icons.Delete)],
    onRowTap: row => Open(row));
```

- Search, sorting (click a header: ascending, descending, off), selection and paging are handled inside the table. You
  supply the rows and hear about changes through `onSelectionChanged` and `onRowTap`.
- `DataColumn<T>.By` sorts by a key (numbers, dates) instead of by the cell text; use the plain constructor for text
  columns, and its `Cell` to draw something other than text.
- `TableSize` is Carbon's row height: `ExtraSmall` 24, `Small` 32, `Medium` 40, `Large` 48, `ExtraLarge` 64.
- Rows are virtualised and the header stays put. Without `maxBodyHeight` the table is as tall as the rows on the page.
- Selecting a row swaps the toolbar for a blue batch bar with your `batchActions` and a Cancel button; the header
  checkbox shows a dash when only some rows are selected.
- Pass `rowDetail` to make rows expandable: a chevron appears in front of each row and the panel you build shows
  under the open row, `detailHeight` tall (96 by default).
- Columns that cannot all fit make the table scroll sideways, header and rows together, while the toolbar and pager stay
  put. A flexible column never gets narrower than its `MinWidth` (120 by default). A vertical mouse wheel over the table
  still scrolls the page.

Not there yet: inline cell editing, column resizing, sticky columns, and rows that size to their expanded content.

using System.Globalization;
using SkiaSharp;
using Sway.Widgets;

namespace Sway.Extras.Ibm;

/// <summary>A Carbon combo box: a text field that filters its list as you type. Choosing an item fills the field; clicking away restores the chosen label.</summary>
public sealed class CarbonComboBox<T>(IReadOnlyList<CarbonDropdownItem<T>> items, T? value = default, Action<T>? onChanged = null, string? label = null,
    string? placeholder = null, string? helperText = null, CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false,
    float menuMaxHeight = 240, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<CarbonDropdownItem<T>> Items => items;
    internal T? Value => value;
    internal Action<T>? OnChanged => onChanged;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal string? HelperText => helperText;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    internal float MenuMaxHeight => menuMaxHeight;
    public override State CreateState() => new CarbonComboBoxState<T>();
}

sealed class CarbonComboBoxState<T> : State<CarbonComboBox<T>> where T : notnull
{
    readonly TextEditingController _text = new();
    readonly FocusNode _node = new() { DebugLabel = "CarbonComboBox" };
    OverlayEntry? _entry;
    string _filter = "";
    int _highlight;
    bool _hover;

    string SelectedLabel => Widget.Items.FirstOrDefault(i => EqualityComparer<T>.Default.Equals(i.Value, Widget.Value))?.Label ?? "";

    List<CarbonDropdownItem<T>> Matches() => _filter.Length == 0 ? Widget.Items.ToList()
        : Widget.Items.Where(i => i.Label.Contains(_filter, StringComparison.CurrentCultureIgnoreCase)).ToList();

    public override void InitState()
    {
        _text.Text = SelectedLabel;
        _node.Changed += OnFocus;
    }

    public override void DidUpdateWidget(CarbonComboBox<T> old)
    {
        if (!_node.HasFocus && _entry is null) _text.Text = SelectedLabel;
    }

    public override void Dispose()
    {
        _node.Changed -= OnFocus;
        CloseMenu();
    }

    void OnFocus() { if (Mounted) SetState(); }

    void CloseMenu()
    {
        _entry?.Remove();
        _entry = null;
        _filter = "";
    }

    void Close()
    {
        CloseMenu();
        _text.Text = SelectedLabel;
        if (Mounted) SetState();
    }

    void OpenMenu()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box) return;
        float item = (float)Widget.Size;
        _entry = CarbonPopup.Open(Context, box, MenuPanel, Math.Min(Widget.MenuMaxHeight, Math.Max(1, Matches().Count) * item), Close, takeFocus: false);
        SetState();
    }

    void Pick(CarbonDropdownItem<T> item)
    {
        _text.Text = item.Label;
        CloseMenu();
        SetState();
        Widget.OnChanged?.Invoke(item.Value);
    }

    void Typed(string s)
    {
        _filter = s;
        _highlight = 0;
        if (_entry is null) OpenMenu(); else _entry.MarkNeedsBuild();
    }

    bool OnKey(KeyEvent e)
    {
        if (!e.IsDown) return false;
        var matches = Matches();
        switch (e.Key)
        {
            case "ArrowDown":
                if (_entry is null) { OpenMenu(); return true; }
                _highlight = Math.Min(matches.Count - 1, _highlight + 1);
                _entry.MarkNeedsBuild();
                return true;
            case "ArrowUp":
                _highlight = Math.Max(0, _highlight - 1);
                _entry?.MarkNeedsBuild();
                return _entry is not null;
            case "Enter" when _entry is not null:
                if (_highlight >= 0 && _highlight < matches.Count && matches[_highlight].Enabled) Pick(matches[_highlight]);
                return true;
            case "Escape" when _entry is not null:
                Close();
                return true;
        }
        return false;
    }

    Widget MenuPanel()
    {
        var theme = CarbonTheme.Of(Context);
        var c = theme.Colors;
        var matches = Matches();
        float item = (float)Widget.Size;
        Widget list = matches.Count == 0
            ? new Container(height: item, padding: EdgeInsets.Symmetric(horizontal: 16), alignment: AlignmentDirectional.CenterStart,
                child: new Text(CarbonLocalizations.Of(Context).NoMatchingResults, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextSecondary))))
            : new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: matches.Select((m, i) =>
            {
                bool selected = EqualityComparer<T>.Default.Equals(m.Value, Widget.Value);
                return (Widget)new SizedBox(height: item, child: new Interactive((ctx, st) => new Container(padding: EdgeInsets.Symmetric(horizontal: 16),
                    color: _highlight == i || st.Hover ? c.LayerHover01 : Colors.Transparent, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
                    [
                        new Expanded(new Text(m.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                            style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: m.Enabled ? c.TextPrimary : c.TextDisabled)))),
                        ..selected ? [new Icon(Icons.Check, 16, c.IconPrimary)] : Array.Empty<Widget>(),
                    ])), m.Enabled ? () => Pick(m) : null, focusable: false));
            }).ToList());
        return CarbonPopup.Shadowed(theme, new ConstrainedBox(new BoxConstraints(0, float.PositiveInfinity, 0, Widget.MenuMaxHeight), new SingleChildScrollView(list, shrinkWrap: true)));
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.OnChanged is not null;
        float height = (float)Widget.Size;
        bool open = _entry is not null;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled));

        Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Expanded(new GestureDetector(onTap: enabled ? () => { _node.RequestFocus(); OpenMenu(); } : null, behavior: HitTestBehavior.Opaque,
                child: new Padding(EdgeInsets.Symmetric(horizontal: 16), new Align(AlignmentDirectional.CenterStart,
                    new EditableText(_text, _node, text, text.Merge(new TextStyle(Color: c.TextPlaceholder)), Widget.Placeholder ?? CarbonLocalizations.Of(Context).FilterPlaceholder, false, 1, null, !enabled,
                        c.TextPrimary, c.Highlight, Typed))))),
            new GestureDetector(onTap: enabled ? () => { if (open) Close(); else { _node.RequestFocus(); OpenMenu(); } } : null, behavior: HitTestBehavior.Opaque,
                child: new SizedBox(width: height, height: height, child: new Center(new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 16, enabled ? c.IconPrimary : c.IconDisabled)))),
        ]);
        Widget field = new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new Focus(onKey: OnKey, child: CarbonField.Frame(theme, height, _node.HasFocus || open, _hover && enabled, false, enabled, Widget.OnLayer ? c.Field02 : null, content)));

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            ..Widget.Label is null ? Array.Empty<Widget>() : [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
            field,
            ..Widget.HelperText is { } h ? [new Text(h, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextHelper)))] : Array.Empty<Widget>(),
        ]);
    }
}

/// <summary>A Carbon multi-select: a dropdown whose items are checkboxes. The menu stays open while you pick, and a count badge clears the lot.</summary>
public sealed class CarbonMultiSelect<T>(IReadOnlyList<CarbonDropdownItem<T>> items, IReadOnlyCollection<T> selected, Action<IReadOnlyList<T>>? onChanged = null,
    string? label = null, string? placeholder = null, CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false,
    float menuMaxHeight = 240, Key? key = null) : StatefulWidget(key) where T : notnull
{
    internal IReadOnlyList<CarbonDropdownItem<T>> Items => items;
    internal IReadOnlyCollection<T> Selected => selected;
    internal Action<IReadOnlyList<T>>? OnChanged => onChanged;
    internal string? Label => label;
    internal string? Placeholder => placeholder;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    internal float MenuMaxHeight => menuMaxHeight;
    public override State CreateState() => new CarbonMultiSelectState<T>();
}

sealed class CarbonMultiSelectState<T> : State<CarbonMultiSelect<T>> where T : notnull
{
    OverlayEntry? _entry;
    readonly FocusNode _node = new() { DebugLabel = "CarbonMultiSelect" };
    // The newest selection this control produced, so quick consecutive picks do not overwrite each other before the owner rebuilds.
    List<T>? _pending;

    List<T> Current => _pending ?? Widget.Selected.ToList();

    public override void DidUpdateWidget(CarbonMultiSelect<T> old)
    {
        _pending = null;
        _entry?.MarkNeedsBuild();
    }

    public override void Dispose() => Close(false);

    void Close(bool refocus = true)
    {
        _entry?.Remove();
        _entry = null;
        if (Mounted) SetState();
        if (refocus && Mounted) _node.RequestFocus();
    }

    void Emit(List<T> next)
    {
        _pending = next;
        Widget.OnChanged?.Invoke(next);
        _entry?.MarkNeedsBuild();
        if (Mounted) SetState();
    }

    void Toggle(T value)
    {
        var next = Current;
        if (!next.Remove(value)) next.Add(value);
        Emit(next);
    }

    void Open()
    {
        if (_entry is not null || Context.FindRenderObject() is not RenderBox box) return;
        float item = (float)Widget.Size;
        _entry = CarbonPopup.Open(Context, box, MenuPanel, Math.Min(Widget.MenuMaxHeight, Widget.Items.Count * item), () => Close());
        SetState();
    }

    Widget MenuPanel()
    {
        var theme = CarbonTheme.Of(Context);
        var c = theme.Colors;
        float item = (float)Widget.Size;
        var chosen = Current;
        var list = new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, children: Widget.Items.Select(m =>
        {
            bool on = chosen.Contains(m.Value);
            return (Widget)new SizedBox(height: item, child: new Interactive((ctx, st) => new Container(padding: EdgeInsets.Symmetric(horizontal: 16),
                color: st.Hover ? c.LayerHover01 : Colors.Transparent, child: new Row(crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                [
                    new CarbonCheckbox(on),
                    new Expanded(new Text(m.Label, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1,
                        style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: m.Enabled ? c.TextPrimary : c.TextDisabled)))),
                ])), m.Enabled ? () => Toggle(m.Value) : null, focusable: false));
        }).ToList());
        return CarbonPopup.Shadowed(theme, new ConstrainedBox(new BoxConstraints(0, float.PositiveInfinity, 0, Widget.MenuMaxHeight), new SingleChildScrollView(list, shrinkWrap: true)));
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.OnChanged is not null;
        float height = (float)Widget.Size;
        bool open = _entry is not null;
        var chosen = Current;
        var names = Widget.Items.Where(i => chosen.Contains(i.Value)).Select(i => i.Label).ToList();

        Widget badge = new Container(height: 24, padding: EdgeInsets.Symmetric(horizontal: 8), decoration: new BoxDecoration(Color: c.BackgroundInverse, BorderRadius: BorderRadius.Circular(12)),
            child: new Row(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Center, spacing: 4, children:
            [
                new Text(chosen.Count.ToString(), style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextInverse))),
                new GestureDetector(onTap: enabled ? () => Emit([]) : null, behavior: HitTestBehavior.Opaque, child: new Icon(Icons.Close, 12, c.IconInverse)),
            ]));

        Widget field = new Interactive((ctx, st) =>
        {
            Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                ..chosen.Count > 0 ? [new Padding(EdgeInsets.Only(left: 16), badge)] : Array.Empty<Widget>(),
                new Expanded(new Padding(EdgeInsets.Symmetric(horizontal: 16), new Text(names.Count > 0 ? string.Join(", ", names) : Widget.Placeholder ?? CarbonLocalizations.Of(context).ChooseOptions, softWrap: false,
                    overflow: TextOverflow.Ellipsis, maxLines: 1,
                    style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: !enabled ? c.TextDisabled : names.Count == 0 ? c.TextPlaceholder : c.TextPrimary))))),
                new SizedBox(width: height, height: height, child: new Center(new Icon(open ? Icons.ExpandLess : Icons.ExpandMore, 16, enabled ? c.IconPrimary : c.IconDisabled))),
            ]);
            return CarbonField.Frame(theme, height, open || st.FocusVisible, st.Hover && enabled, false, enabled, Widget.OnLayer ? c.Field02 : null, content);
        }, enabled ? () => { if (_entry is null) Open(); else Close(); } : null, focusNode: _node);

        return new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            ..Widget.Label is null ? Array.Empty<Widget>() : [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
            field,
        ]);
    }
}

/// <summary>A month calendar that picks one day. It is the panel inside <see cref="CarbonDatePicker"/> and can be used on its own.</summary>
public sealed class CarbonCalendar(DateTime? selected, Action<DateTime> onPicked, DateTime? min = null, DateTime? max = null, DateTime? today = null, Key? key = null) : StatefulWidget(key)
{
    internal DateTime? Selected => selected;
    internal Action<DateTime> OnPicked => onPicked;
    internal DateTime? Min => min;
    internal DateTime? Max => max;
    internal DateTime Today => (today ?? DateTime.Today).Date;
    public override State CreateState() => new CarbonCalendarState();
}

sealed class CarbonCalendarState : State<CarbonCalendar>
{
    const float Cell = 40;
    DateTime _month;

    public override void InitState()
    {
        var anchor = Widget.Selected ?? Widget.Today;
        _month = new DateTime(anchor.Year, anchor.Month, 1);
    }

    void Move(int months) => SetState(() => _month = _month.AddMonths(months));

    bool Allowed(DateTime d) => (Widget.Min is null || d >= Widget.Min.Value.Date) && (Widget.Max is null || d <= Widget.Max.Value.Date);

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        var culture = Localizations.LocaleOf(context).Culture;
        var first = culture.DateTimeFormat.FirstDayOfWeek;
        int lead = ((int)_month.DayOfWeek - (int)first + 7) % 7;
        var start = _month.AddDays(-lead);

        Widget Nav(IconData icon, int by) => new Interactive((ctx, st) => CarbonFocus.Around(st.FocusVisible, theme, new Container(width: Cell, height: Cell,
            color: st.Hover ? c.LayerHover01 : Colors.Transparent, child: new Center(new Icon(icon, 16, c.IconPrimary)))), () => Move(by));

        var week = new List<Widget>();
        for (int i = 0; i < 7; i++)
            week.Add(new Container(width: Cell, height: Cell, alignment: Alignment.Center, child: new Text(culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(((int)first + i) % 7))[..2],
                style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary)))));

        var rows = new List<Widget>();
        for (int r = 0; r < 6; r++)
        {
            var days = new List<Widget>();
            for (int d = 0; d < 7; d++)
            {
                var day = start.AddDays(r * 7 + d);
                bool outside = day.Month != _month.Month, picked = Widget.Selected?.Date == day, now = day == Widget.Today, ok = Allowed(day);
                days.Add(new Interactive((ctx, st) => new Container(width: Cell, height: Cell, alignment: Alignment.Center,
                    decoration: new BoxDecoration(Color: picked ? c.ButtonPrimary : st.Hover && ok ? c.LayerHover01 : Colors.Transparent,
                        Border: now && !picked ? Border.Only(bottom: new BorderSide(c.BorderInteractive, 2)) : default),
                    child: new Text(day.Day.ToString(), style: theme.Type.BodyCompact01.Merge(new TextStyle(
                        Color: picked ? c.TextOnColor : !ok ? c.TextDisabled : outside ? c.TextSecondary : c.TextPrimary, FontWeight: picked || now ? FontWeight.W600 : FontWeight.W400)))),
                    ok ? () => Widget.OnPicked(day) : null, focusable: false));
            }
            rows.Add(new Row(mainAxisSize: MainAxisSize.Min, children: days));
        }

        return new Container(width: Cell * 7, color: c.Layer01, child: new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Row(children:
            [
                Nav(Icons.ChevronLeft, -1),
                new Expanded(new Center(new Text(_month.ToString("MMMM yyyy", culture), style: theme.Type.HeadingCompact01.Merge(new TextStyle(Color: c.TextPrimary))))),
                Nav(Icons.ChevronRight, 1),
            ]),
            new Row(mainAxisSize: MainAxisSize.Min, children: week),
            ..rows,
        ]));
    }
}

/// <summary>A Carbon date picker: a field you can type a date into, with a calendar that opens beneath it. Dates are written month/day/year.</summary>
public sealed class CarbonDatePicker(DateTime? value, Action<DateTime?>? onChanged = null, string? label = null, string? helperText = null,
    DateTime? min = null, DateTime? max = null, CarbonFieldSize size = CarbonFieldSize.Medium, bool onLayer = false, Key? key = null) : StatefulWidget(key)
{
    internal const string Format = "MM/dd/yyyy";
    internal DateTime? Value => value;
    internal Action<DateTime?>? OnChanged => onChanged;
    internal string? Label => label;
    internal string? HelperText => helperText;
    internal DateTime? Min => min;
    internal DateTime? Max => max;
    internal CarbonFieldSize Size => size;
    internal bool OnLayer => onLayer;
    public override State CreateState() => new CarbonDatePickerState();
}

sealed class CarbonDatePickerState : State<CarbonDatePicker>
{
    readonly TextEditingController _text = new();
    readonly FocusNode _node = new() { DebugLabel = "CarbonDatePicker" };
    OverlayEntry? _entry;
    bool _hover, _invalid;

    string Shown => Widget.Value?.ToString(CarbonDatePicker.Format, CultureInfo.InvariantCulture) ?? "";

    public override void InitState()
    {
        _text.Text = Shown;
        _node.Changed += OnFocus;
    }

    public override void DidUpdateWidget(CarbonDatePicker old)
    {
        if (Widget.Value != old.Value && !_node.HasFocus) { _text.Text = Shown; _invalid = false; }
    }

    public override void Dispose()
    {
        _node.Changed -= OnFocus;
        CloseCalendar();
    }

    void OnFocus()
    {
        if (!Mounted) return;
        if (!_node.HasFocus && _entry is null) Commit();
        SetState();
    }

    void CloseCalendar()
    {
        _entry?.Remove();
        _entry = null;
    }

    // Reads whatever was typed: empty clears the date, a valid date in range is reported, anything else flags the field.
    void Commit()
    {
        var s = _text.Text.Trim();
        if (s.Length == 0) { _invalid = false; if (Widget.Value is not null) Widget.OnChanged?.Invoke(null); return; }
        if (DateTime.TryParseExact(s, CarbonDatePicker.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            && (Widget.Min is null || d >= Widget.Min.Value.Date) && (Widget.Max is null || d <= Widget.Max.Value.Date))
        {
            _invalid = false;
            if (Widget.Value?.Date != d) Widget.OnChanged?.Invoke(d);
        }
        else _invalid = true;
    }

    void Pick(DateTime d)
    {
        CloseCalendar();
        _invalid = false;
        _text.Text = d.ToString(CarbonDatePicker.Format, CultureInfo.InvariantCulture);
        SetState();
        Widget.OnChanged?.Invoke(d);
    }

    void Toggle()
    {
        if (_entry is not null) { CloseCalendar(); SetState(); return; }
        if (Context.FindRenderObject() is not RenderBox box) return;
        DateTime? typed = DateTime.TryParseExact(_text.Text.Trim(), CarbonDatePicker.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : Widget.Value;
        _entry = CarbonPopup.Open(Context, box, () => CarbonPopup.Shadowed(CarbonTheme.Of(Context), new CarbonCalendar(typed, Pick, Widget.Min, Widget.Max)),
            40 * 8, () => { CloseCalendar(); if (Mounted) SetState(); }, 280);
        SetState();
    }

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        bool enabled = Widget.OnChanged is not null;
        float height = (float)Widget.Size;
        var text = theme.Type.BodyCompact01.Merge(new TextStyle(Color: enabled ? c.TextPrimary : c.TextDisabled));

        Widget content = new Row(crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new Expanded(new GestureDetector(onTap: enabled ? () => _node.RequestFocus() : null, behavior: HitTestBehavior.Opaque,
                child: new Padding(EdgeInsets.Symmetric(horizontal: 16), new Align(AlignmentDirectional.CenterStart,
                    new EditableText(_text, _node, text, text.Merge(new TextStyle(Color: c.TextPlaceholder)), "mm/dd/yyyy", false, 1, null, !enabled,
                        c.TextPrimary, c.Highlight, _ => { if (_invalid) SetState(() => _invalid = false); }, _ => Commit()))))),
            new GestureDetector(onTap: enabled ? Toggle : null, behavior: HitTestBehavior.Opaque,
                child: new SizedBox(width: height, height: height, child: new Center(new Icon(Icons.CalendarToday, 16, enabled ? c.IconPrimary : c.IconDisabled)))),
        ]);

        return new MouseRegion(onEnter: _ => SetState(() => _hover = true), onExit: _ => SetState(() => _hover = false), opaque: false,
            child: new Column(crossAxisAlignment: CrossAxisAlignment.Stretch, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
            [
                ..Widget.Label is null ? Array.Empty<Widget>() : [new Text(Widget.Label, style: theme.Type.Label01.Merge(new TextStyle(Color: enabled ? c.TextSecondary : c.TextDisabled)))],
                CarbonField.Frame(theme, height, _node.HasFocus || _entry is not null, _hover && enabled, _invalid, enabled, Widget.OnLayer ? c.Field02 : null, content),
                ..(_invalid ? [new Text(CarbonLocalizations.Of(context).EnterDateAs("mm/dd/yyyy"), style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextError)))]
                    : Widget.HelperText is { } h ? [new Text(h, style: theme.Type.HelperText01.Merge(new TextStyle(Color: c.TextHelper)))] : Array.Empty<Widget>()),
            ]));
    }
}

/// <summary>
/// A Carbon file uploader: a heading, a description and a button that opens the platform's file picker. Chosen files are listed with a button to remove each.
/// The control holds no state of its own: the owner keeps <paramref name="files"/> and updates it from <paramref name="onChanged"/>.
/// </summary>
public sealed class CarbonFileUploader(IReadOnlyList<PickedFile> files, Action<IReadOnlyList<PickedFile>> onChanged, string? label = null,
    string? description = null, string? buttonLabel = null, bool multiple = true, IReadOnlyList<FileTypeFilter>? filters = null, Key? key = null) : StatelessWidget(key)
{
    static string Size(long? bytes) => bytes switch
    {
        null => "",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} kB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };

    public override Widget Build(BuildContext context)
    {
        var theme = CarbonTheme.Of(context);
        var c = theme.Colors;
        void Pick() => FilePicker.PickFiles(picked =>
        {
            if (picked.Count == 0) return;
            onChanged(multiple ? [.. files, .. picked] : [picked[0]]);
        }, new FilePickerOptions { AllowMultiple = multiple, Filters = filters });

        return new Column(crossAxisAlignment: CrossAxisAlignment.Start, mainAxisSize: MainAxisSize.Min, spacing: 8, children:
        [
            new Text(label ?? CarbonLocalizations.Of(context).UploadFiles, style: theme.Type.Heading03.Merge(new TextStyle(Color: c.TextPrimary))),
            ..description is null ? Array.Empty<Widget>() : [new Text(description, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextSecondary)))],
            new Padding(EdgeInsets.Only(top: 8, bottom: 8), new CarbonButton(new Text(buttonLabel ?? CarbonLocalizations.Of(context).AddFile), Pick, CarbonButtonKind.Tertiary, CarbonButtonSize.Medium, Icons.Add)),
            ..files.Select((f, i) => (Widget)new SizedBox(width: 320, child: new Container(height: 48, color: c.Layer01, padding: EdgeInsets.Only(left: 16), child: new Row(
                crossAxisAlignment: CrossAxisAlignment.Center, spacing: 8, children:
                [
                    new Expanded(new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                    [
                        new Text(f.Name, softWrap: false, overflow: TextOverflow.Ellipsis, maxLines: 1, style: theme.Type.BodyCompact01.Merge(new TextStyle(Color: c.TextPrimary))),
                        ..f.Length is null ? Array.Empty<Widget>() : [new Text(Size(f.Length), style: theme.Type.Label01.Merge(new TextStyle(Color: c.TextSecondary)))],
                    ])),
                    new CarbonIconButton(Icons.Close, () => onChanged(files.Where((_, n) => n != i).ToList()), CarbonButtonSize.Large),
                ])))),
        ]);
    }
}

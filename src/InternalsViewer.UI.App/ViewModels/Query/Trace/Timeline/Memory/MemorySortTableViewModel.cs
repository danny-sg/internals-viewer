using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed class MemorySortTableViewModel : MemoryStructureViewModel
{
    private const string SortTableName = "Sort Table";

    private const string RowsetName = "Rowset";

    private const string DescriptorName = "Sort Descriptor";

    private const string RunsName = "Runs";

    private const string RecordPagesName = "Record Pages";

    private const string RecordPageName = "Record Page";

    private const string ComparePagesName = "Compare Pages";

    private const string ComparePageName = "Compare Page";

    private const string MemoryName = "Memory";

    private const string CursorBadge = "Cursor";

    private const string NotReadable = "No Sort Table At This Position";

    private const string NoRecord = "No Record For This Entry";

    private const int EntryWidth = 140;

    private const int NumberWidth = 80;

    private const int RunWidth = 100;

    private const int KeyWidth = 240;

    private const int RecordLengthWidth = 140;

    private const int KeyLengthWidth = 120;

    private const int ColumnWidth = 120;

    private const int SlotCountOffset = 0x16;

    private const int MaximumKeyBytes = 32;

    private const byte FirstPrintable = 0x20;

    private const byte LastPrintable = 0x7E;

    public MemorySortTableViewModel(MemoryHistoryViewModel history, ulong iterator, TracePosition position, ITraceNavigator navigator)
        : base(history, position, navigator)
    {
        Iterator = iterator;

        SelectedKey = Node(MemoryStructureNodeType.SortTable, SortTableName, iterator, null).Key;

        Loader = new PlayheadLoader<LoadedTable?>(LoadInterval, Apply);

        Pages = TraceSortTableReader.GetPages(history.Store, history.AddressMap, iterator);

        Load();
    }

    public override string Title => $"Sort Table 0x{Iterator:X16}";

    public override string Key => $"SortTable:{Iterator:X}";

    protected override HexImage? Image => Loaded?.Image;

    private ulong Iterator { get; }

    private EngineLayout Layout => History.Store.Layout;

    private IReadOnlyList<MemoryField> SortIteratorFields => field ??= Layout.GetFields(TraceSortTableReader.IteratorClass);

    private IReadOnlyList<MemoryField> EntryFields => Layout.SortEntry.Fields;

    private List<Colour> FieldColours => field ??= CreateColours(new[]
    {
        SortIteratorFields.Count,
        Layout.SortRowset.Fields.Count,
        Layout.SortDescriptor.Fields.Count,
        EntryFields.Count
    }.Max());

    private int CursorColour => GetField(Layout.SortDescriptor.Fields, Layout.SortDescriptor.CursorField);

    private PlayheadLoader<LoadedTable?> Loader { get; }

    private List<(int Block, TraceSortPartType Type)> Pages { get; }

    private LoadedTable? Loaded { get; set; }

    private TraceSortPart? PendingPart { get; set; }

    private Task<List<TraceMemoryUsage>> MemoryUsage
        => field ??= Task.Run(() => TraceSortTableReader.GetMemory(History.Store, History.AddressMap, Iterator));

    public bool Contains(ulong iterator) => Iterator == iterator;

    public void ShowPart(TraceSortPart part)
    {
        PendingPart = part;

        if (Loaded is not null)
        {
            SelectPendingPart();
        }
    }

    public override void SelectOffset(int offset)
    {
        if (SelectedNode is not { } node || Loaded is not { } loaded)
        {
            return;
        }

        var previous = MaskedMarker;

        if (node.Type is MemoryStructureNodeType.SortTable or MemoryStructureNodeType.Rowset or MemoryStructureNodeType.Descriptor)
        {
            SelectFieldAt(offset);
        }
        else if (node.Type is MemoryStructureNodeType.ComparePage or MemoryStructureNodeType.Run)
        {
            var size = Layout.SortEntry.Size;

            var entry = HexAddress + (ulong)((offset - loaded.Image.Offset) / size * size);

            if (EntryTable?.Rows.FirstOrDefault(r => r.Id == (long)entry) is { } row)
            {
                SelectedEntryRow = row;
            }

            return;
        }
        else if (node.Type == MemoryStructureNodeType.RecordPage)
        {
            Page.SelectContaining(offset);
        }

        MaskAt(offset, previous);
    }

    public override void Close()
    {
        Loader.Cancel();

        Loaded = null;

        base.Close();
    }

    protected override void Load()
    {
        var history = History;

        var iterator = Iterator;

        var pages = Pages;

        var position = Position;

        var hexAddress = HexAddress;

        Loader.Request(() => Read(history, iterator, pages, position, hexAddress));
    }

    protected override void ShowDetail(MemoryStructureNode node)
    {
        if (Loaded is not { } loaded)
        {
            return;
        }

        switch (node.Type)
        {
            case MemoryStructureNodeType.SortTable:
                ShowFields(node, SortIteratorFields, FieldColours);
                break;

            case MemoryStructureNodeType.Rowset:
                ShowFields(node, Layout.SortRowset.Fields, FieldColours);
                break;

            case MemoryStructureNodeType.Descriptor:
                ShowDescriptor(node);
                break;

            case MemoryStructureNodeType.Run:
                ShowRun(loaded, node);
                break;

            case MemoryStructureNodeType.ComparePage:
                ShowComparePage(loaded, node);
                break;

            case MemoryStructureNodeType.RecordPage:
                ShowPage(null);
                break;

            case MemoryStructureNodeType.Memory:
                ShowMemory(MemoryUsage);
                break;

            default:
                ClearDetail();
                break;
        }
    }

    protected override ResultDetails GetEntryDetails(ResultRow<long> row)
    {
        if (Loaded?.Table.GetEntry((ulong)row.Id) is not { } entry || ReadRecord(entry.Record) is not { Count: > 0 } values)
        {
            return new ResultDetails(string.Empty, [], NoRecord);
        }

        var columns = values.Select((v, i) => new ResultColumn(i, v.Name, typeof(string), i == values.Count - 1) { Width = ColumnWidth })
                            .ToList();

        var rows = new List<ResultRow<long>> { new([.. values.Select(v => (object?)v.Value)]) { Id = 0 } };

        return new ResultDetails(string.Empty, []) { ResultSet = new QueryResultSet { Columns = columns, Rows = rows } };
    }

    protected override ulong GetHexAddress(MemoryStructureNode node)
        => node.Type == MemoryStructureNodeType.Run ? GetEntryPage(SelectedItem ?? GetRunHead(node.Address)) : base.GetHexAddress(node);

    protected override MemoryStructureNode? GetPointerNode(ulong value)
    {
        if (Loaded?.Table.Runs.FirstOrDefault(r => r.Head == value) is { } run
            && Roots.SelectMany(Flatten)
                    .FirstOrDefault(n => n.Type == MemoryStructureNodeType.Run && n.Address == run.Address) is { } found)
        {
            return found;
        }

        return Nodes.TryGetValue(value, out var node)
            ? node
            : Roots.SelectMany(Flatten)
                   .FirstOrDefault(n => n.Type is MemoryStructureNodeType.RecordPage or MemoryStructureNodeType.ComparePage
                                        && IsOnPage(value, n.Address));
    }

    protected override void OpenPointer(MemoryStructureNode node, ulong value)
    {
        switch (node.Type)
        {
            case MemoryStructureNodeType.RecordPage:
                OpenRecord(value);
                break;

            case MemoryStructureNodeType.ComparePage when value != node.Address:
                JumpToEntry(value);
                break;

            case MemoryStructureNodeType.Run:
                Select(node, value);
                break;

            default:
                Select(node);
                break;
        }
    }

    private static LoadedTable? Read(MemoryHistoryViewModel history,
                                     ulong iterator,
                                     List<(int Block, TraceSortPartType Type)> pages,
                                     TracePosition position,
                                     ulong hexAddress)
    {
        var store = history.Store;

        if (TraceSortTableReader.Read(store, pages, iterator, position) is not { } table)
        {
            return null;
        }

        var memory = new MemoryHistoryReader(store, position);

        var keys = new Dictionary<ulong, string>();

        foreach (var entry in table.Entries)
        {
            keys[entry.Address] = ReadKey(memory, entry);
        }

        var rows = new Dictionary<ulong, int>();

        foreach (var page in table.RecordPages)
        {
            rows[page] = memory.TryRead(page + SlotCountOffset, out ushort slots) ? slots : 0;
        }

        return new LoadedTable(table, keys, rows, HexImage.Read(history, hexAddress, position));
    }

    private static string ReadKey(MemoryHistoryReader memory, TraceSortEntry entry)
    {
        var length = Math.Clamp(entry.KeyLength, 0, MaximumKeyBytes);

        var bytes = new byte[length];

        if (length == 0 || entry.Key == 0 || memory.Read(entry.Key, bytes) != length)
        {
            return string.Empty;
        }

        return Array.TrueForAll(bytes, b => b is >= FirstPrintable and <= LastPrintable)
            ? $"'{string.Concat(bytes.Select(b => (char)b))}'"
            : Convert.ToHexString(bytes);
    }

    private static int GetField(IReadOnlyList<MemoryField> fields, MemoryField field)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (ReferenceEquals(fields[index], field))
            {
                return index;
            }
        }

        return 0;
    }

    private static string GetCountText(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count:N0} {noun}s";

    private static string GetText(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private int GetFieldWidth(MemoryField field) => field switch
    {
        _ when field == Layout.SortEntry.KeyField => KeyWidth,
        _ when field == Layout.SortEntry.RecordLengthField => RecordLengthWidth,
        _ when field == Layout.SortEntry.KeyLengthField => KeyLengthWidth,
        _ => AddressWidth
    };

    private static int GetRun(TraceSortTable table, ulong address)
    {
        for (var index = 0; index < table.Runs.Count; index++)
        {
            if (table.Runs[index].Address == address)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsOnPage(ulong address, ulong page) => address >= page && address < page + PageSize;

    private static MemoryStructureNode Node(MemoryStructureNodeType type, string name, ulong address, MemoryStructureNode? parent)
        => new(type, name, address, -1, parent);

    private MemoryStructureBadge GetCursorBadge() => new(CursorBadge, FieldColours[CursorColour].ToWindowsColor());

    private IEnumerable<Marker> GetEntryMarkers(List<TraceSortEntry> entries, ulong page, int hexBase)
    {
        foreach (var entry in entries)
        {
            var offset = hexBase + (int)(entry.Address - page);

            for (var index = 0; index < EntryFields.Count; index++)
            {
                var field = EntryFields[index];

                var name = $"Entry {entry.Index + 1} {field.Name}";

                yield return CreateMarker(name, offset + field.Offset, field.Length, FieldColours[index]);
            }
        }
    }

    private static object GetEntryCell(TraceSortTable table, TraceSortEntry entry)
    {
        var text = $"Entry {entry.Index + 1:N0}";

        return entry.Address == table.Cursor ? new BadgeText([], text, [CursorBadge]) : text;
    }

    private void Apply(LoadedTable? loaded)
    {
        Loaded = loaded;

        if (loaded is null)
        {
            ClearDetail();

            EmptyText = NotReadable;

            return;
        }

        IsApplying = true;

        BuildTree(loaded);

        IsApplying = false;

        if (PendingPart is null)
        {
            RememberFirst();
        }

        if (PendingPart is not null)
        {
            SelectPendingPart();

            return;
        }

        Show();
    }

    private void BuildTree(LoadedTable loaded)
    {
        var table = loaded.Table;

        var root = Node(MemoryStructureNodeType.SortTable, SortTableName, table.Iterator, null);

        root.Children.Add(Node(MemoryStructureNodeType.Memory, MemoryName, 0, root));

        var rowset = Node(MemoryStructureNodeType.Rowset, RowsetName, table.Rowset, root);

        if (table.Descriptor != 0)
        {
            rowset.Children.Add(Node(MemoryStructureNodeType.Descriptor, DescriptorName, table.Descriptor, rowset));
        }

        root.Children.Add(rowset);

        if (table.Runs.Count > 0)
        {
            var heading = Node(MemoryStructureNodeType.Runs, RunsName, 0, root);

            var entries = table.Entries.Where(e => e.Run >= 0).ToLookup(e => e.Run);

            for (var index = 0; index < table.Runs.Count; index++)
            {
                var run = table.Runs[index];

                var hasCursor = run.Field != Layout.SortDescriptor.CursorField && entries[index].Any(e => e.Address == table.Cursor);

                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.Run, run.Field.Name, run.Address, -1, heading)
                {
                    Detail = GetCountText(entries[index].Count(), "Entry"),
                    Badges = hasCursor ? [GetCursorBadge()] : []
                });
            }

            root.Children.Add(heading);
        }

        if (table.RecordPages.Count > 0)
        {
            var heading = Node(MemoryStructureNodeType.RecordPages, RecordPagesName, 0, root);

            foreach (var page in table.RecordPages)
            {
                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.RecordPage, RecordPageName, page, -1, heading)
                {
                    Detail = GetCountText(loaded.PageRows.GetValueOrDefault(page), "Row")
                });
            }

            root.Children.Add(heading);
        }

        if (table.ComparePages.Count > 0)
        {
            var heading = Node(MemoryStructureNodeType.ComparePages, ComparePagesName, 0, root);

            foreach (var page in table.ComparePages)
            {
                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.ComparePage, ComparePageName, page, -1, heading)
                {
                    Detail = GetCountText(table.GetEntries(page).Count(), "Entry"),
                    Badges = IsOnPage(table.Cursor, page) ? [GetCursorBadge()] : []
                });
            }

            root.Children.Add(heading);
        }

        var key = string.Join("|", Flatten(root).Select(n => $"{n.Key} {n.Detail} {string.Join(",", n.Badges)}"));

        if (key == StructureKey)
        {
            return;
        }

        StructureKey = key;

        var nodes = new Dictionary<ulong, MemoryStructureNode>();

        foreach (var node in Flatten(root).Where(n => n.HasData))
        {
            nodes.TryAdd(node.Address, node);
        }

        Nodes = nodes;

        Roots = [root];

        SelectedNode = Find(SelectedKey) ?? root;

        SelectedKey = SelectedNode.Key;
    }

    private void SelectPendingPart()
    {
        if (PendingPart is not { } part)
        {
            return;
        }

        PendingPart = null;

        if (GetPointerNode(part.Address) is { } node)
        {
            OpenPointer(node, part.Address);

            return;
        }

        Show();
    }

    private void ShowDescriptor(MemoryStructureNode node)
    {
        var fields = Layout.SortDescriptor.Fields;

        var data = Image?.Data;

        var offset = Image?.Offset ?? 0;

        List<MemoryField> shown =
        [
            .. fields.Where(f => !Layout.SortDescriptor.RunFields.Contains(f)
                                 || ReadValue(data, offset + f.Offset, f.Length) is not (null or 0))
        ];

        ShowFields(node, shown, [.. shown.Select(f => FieldColours[GetField(fields, f)])]);
    }

    private void ShowRun(LoadedTable loaded, MemoryStructureNode node)
    {
        FieldTable = null;

        IsPageVisible = false;

        ShownFields = [];

        var run = GetRun(loaded.Table, node.Address);

        List<TraceSortEntry> entries = run < 0 ? [] : [.. loaded.Table.Entries.Where(e => e.Run == run).OrderBy(e => e.SortedIndex)];

        var key = $"{node.Key} {loaded.Table.Cursor:X} {string.Join(",", entries.Select(e => e.Address))}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = CreateEntryTable(loaded, entries, isRun: true);
        }

        SelectItemRow();

        var page = HexAddress;

        var hexBase = loaded.Image.Offset;

        var onPage = entries.Where(e => IsOnPage(e.Address, page)).ToList();

        ShowItemMarkers($"{node.Key} {page:X} {string.Join(",", onPage.Select(e => e.Address))}",
                        () => GetEntryMarkers(onPage, page, hexBase),
                        Layout.SortEntry.Size);
    }

    private ulong GetEntryPage(ulong entry) => Loaded?.Table.ComparePages.FirstOrDefault(p => IsOnPage(entry, p)) ?? 0;

    private ulong GetRunHead(ulong address) => Loaded?.Table.Runs.FirstOrDefault(r => r.Address == address)?.Head ?? 0;

    private void ShowComparePage(LoadedTable loaded, MemoryStructureNode node)
    {
        FieldTable = null;

        IsPageVisible = false;

        ShownFields = [];

        var page = node.Address;

        var hexBase = loaded.Image.Offset;

        var entries = loaded.Table.GetEntries(page).ToList();

        var content = string.Join(",", entries.Select(e => $"{e.Address:X}/{e.Next:X}/{e.Run}/{e.SortedIndex}"));

        var key = $"{node.Key} {loaded.Table.Cursor:X} {content}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = CreateEntryTable(loaded, entries, isRun: false);
        }

        SelectItemRow();

        ShowItemMarkers($"{node.Key} {entries.Count}", () => GetEntryMarkers(entries, page, hexBase), Layout.SortEntry.Size);
    }

    private QueryResultSet CreateEntryTable(LoadedTable loaded, List<TraceSortEntry> entries, bool isRun)
    {
        var columns = new List<ResultColumn>();

        if (isRun)
        {
            columns.Add(new ResultColumn(0, "Position", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right });
        }

        columns.Add(new ResultColumn(columns.Count, "Entry", typeof(string), false) { Width = EntryWidth });

        if (!isRun)
        {
            columns.Add(new ResultColumn(columns.Count, "Run", typeof(string), false) { Width = RunWidth });

            columns.Add(new ResultColumn(columns.Count, "Position", typeof(string), false)
            {
                Width = NumberWidth,
                Alignment = ResultAlignment.Right
            });
        }

        for (var index = 0; index < EntryFields.Count; index++)
        {
            var field = EntryFields[index];

            columns.Add(new ResultColumn(columns.Count, field.Name, typeof(string), false)
            {
                Width = GetFieldWidth(field),
                MarkerColour = FieldColours[index]
            });
        }

        var rows = new List<ResultRow<long>>(entries.Count);

        foreach (var entry in entries)
        {
            var cells = new List<object?>();

            if (isRun)
            {
                cells.Add(GetText(entry.SortedIndex + 1));
            }

            cells.Add(GetEntryCell(loaded.Table, entry));

            if (!isRun)
            {
                cells.Add(entry.Run >= 0 ? loaded.Table.Runs[entry.Run].Field.Name : null);

                cells.Add(entry.SortedIndex >= 0 ? GetText(entry.SortedIndex + 1) : null);
            }

            var record = entry.Record;

            cells.Add(new ActionLinks([new ActionLink(GetAddressText(record), () => OpenRecord(record))]));

            cells.Add(loaded.Keys.GetValueOrDefault(entry.Address));

            cells.Add(GetText(entry.RecordLength));

            cells.Add(GetText(entry.KeyLength));

            var next = entry.Next;

            cells.Add(next == 0 ? null : new ActionLinks([new ActionLink(GetAddressText(next), () => JumpToEntry(next))]));

            rows.Add(new ResultRow<long>([.. cells]) { Id = (long)entry.Address });
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private List<(string Name, string Value)>? ReadRecord(ulong record)
    {
        var position = Position;

        var block = History.GetBlock(record, position);

        if (block < 0 || !History.IsPage(block))
        {
            return null;
        }

        var store = History.Store;

        var count = store.GetWriteCount(block, position);

        var data = count > 0 ? store.GetImage(block, count - 1).Data : null;

        var page = data is null ? null : WorkfilePageReader.Read(data, null, History.GetSortLayout(block));

        var offset = (int)(record - store.Blocks[block].Address);

        return page?.Records.FirstOrDefault(r => r.Offset == offset) is { } found
            ? [.. found.Fields.Select(f => (f.Name, f.Value))]
            : null;
    }

    private void JumpToEntry(ulong address)
    {
        var page = Roots.SelectMany(Flatten)
                        .FirstOrDefault(n => n.Type == MemoryStructureNodeType.ComparePage && IsOnPage(address, n.Address));

        if (page is not null)
        {
            Select(page, address);
        }
    }

    private void OpenRecord(ulong record)
    {
        var page = Roots.SelectMany(Flatten)
                        .FirstOrDefault(n => n.Type == MemoryStructureNodeType.RecordPage && IsOnPage(record, n.Address));

        if (page is not null)
        {
            PendingSlotOffset = (int)(record - page.Address);

            Select(page);

            return;
        }

        var position = Position;

        var block = History.GetBlock(record, position);

        if (block >= 0)
        {
            Navigator.OpenMemory(block, record, position);
        }
    }

    private sealed record LoadedTable(TraceSortTable Table,
                                      Dictionary<ulong, string> Keys,
                                      Dictionary<ulong, int> PageRows,
                                      HexImage Image);
}

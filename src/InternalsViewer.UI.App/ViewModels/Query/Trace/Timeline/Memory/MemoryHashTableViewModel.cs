using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHashTableViewModel : ObservableObject
{
    private const string GroupName = "Partition Group";

    private const string HashTableName = "Hash Table";

    private const string AccessorsName = "Accessors";

    private const string BucketPageName = "Item Page";

    private const string BucketPagesName = "Hash Bucket Pages";

    private const string OverflowPageName = "Overflow Page";

    private const string WorkfilePagesName = "Workfile Pages";

    private const string WorkfilePageName = "Workfile Page";

    private const string InUseBadge = "In Use";

    private const string ActiveBadge = "Active";

    private const string FirstBadge = "First";

    private const string LastBadge = "Last";

    private const string InsertBadge = "Insert";

    private const string MatchBadge = "Match";

    private const string NoMatchBadge = "No Match";

    private const string WorkfileBadge = "Workfile";

    private const string BufferPoolBadge = "Buffer Pool";

    private const string UnknownBadge = "Unknown";

    private const string NotAllocated = "Not Allocated At This Position";

    private const string NotReadable = "No Hash Table At This Position";

    private const string NoRows = "No Rows In This Bucket";

    private const string PointerPrefix = "Pointer: ";

    private const string OpenMemoryCommand = "Open Memory";

    private const string OpenPageCommand = "Open Page";

    private const string SelectCommand = "Select";

    private const int HueWheel = 256;

    private const double GoldenRatio = 0.6180339887;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const int FieldWidth = 200;

    private const int OffsetWidth = 80;

    private const int ValueWidth = 360;

    private const int TypeWidth = 140;

    private const int AccessorWidth = 260;

    private const int BucketWidth = 130;

    private const int RowCountWidth = 90;

    private const int KeyWidth = 100;

    private const int RowWidth = 240;

    private const int ColumnWidth = 120;

    private const int EntryWidth = 70;

    private const int PageSize = 8192;

    private const int FixedEntryColumns = 3;

    private const int CountField = 0;

    private const int HeadField = 1;

    private const int NextField = 2;

    private const int KeyField = 3;

    private const int RowField = 4;

    private static readonly TimeSpan LoadInterval = TimeSpan.FromMilliseconds(50);

    private static readonly int ActiveField = GetField(TraceHashTableLayout.GroupFields, TraceHashTableLayout.ActivePartitionOffset);

    private static readonly int LastField = GetField(TraceHashTableLayout.GroupFields, TraceHashTableLayout.PartitionListOffset);

    private static readonly int FirstField = GetField(TraceHashTableLayout.GroupFields,
                                                      TraceHashTableLayout.PartitionListOffset + sizeof(ulong));

    private static readonly int CurrentField = GetField(TraceHashTableLayout.IteratorFields, TraceHashTableLayout.CurrentGroupOffset);

    private static readonly int ColourCount = new[]
    {
        TraceHashTableLayout.IteratorFields.Count,
        TraceHashTableLayout.GroupFields.Count,
        TraceHashTableLayout.PartitionFields.Count,
        TraceHashTableLayout.ItemFields.Count
    }.Max();

    private static readonly List<Colour> FieldColours =
    [
        .. Enumerable.Range(0, ColourCount).Select(i => ColourHelpers.HsvToColor((int)(i * GoldenRatio * HueWheel) % HueWheel,
                                                                                 FieldSaturation,
                                                                                 FieldValue))
    ];

    [ObservableProperty]
    private List<MemoryHashTableNode> _roots = [];

    [ObservableProperty]
    private MemoryHashTableNode? _selectedNode;

    [ObservableProperty]
    private List<ActionLink> _path = [];

    [ObservableProperty]
    private string _pathTitle = string.Empty;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

    [ObservableProperty]
    private Marker? _selectedMarker;

    [ObservableProperty]
    private int? _scrollToOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldTableVisible))]
    private QueryResultSet? _fieldTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedFieldRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEntryTableVisible))]
    private QueryResultSet? _entryTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedEntryRow;

    [ObservableProperty]
    private bool _isPageVisible;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private List<HexLink>? _links;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    private bool _canGoBack;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private bool _canGoForward;

    public MemoryHashTableViewModel(MemoryHistoryViewModel history, ulong group, TracePosition position, ITraceNavigator navigator)
    {
        History = history;

        Group = group;

        OpenedAt = position;

        Navigator = navigator;

        SelectedKey = new MemoryHashTableNode(MemoryHashTableNodeType.Group, GroupName, group, -1, null).Key;

        WorkfilePage = new MemoryPageViewModel(ShowPageMarkers);

        Loader = new PlayheadLoader<LoadedTable?>(LoadInterval, Apply);

        var calls = history.CallStore;

        var opened = calls is null ? [] : TraceHashTableReader.GetAccesses(calls, group);

        var at = opened.Count > 0 ? opened[0].Position : position;

        Iterator = TraceHashTableReader.GetIterator(history.Store, group, at) ?? 0;

        Groups = Iterator == 0 ? [] : TraceHashTableReader.GetIteratorGroups(history.Store, history.AddressMap, Iterator, at);

        if (!Groups.Exists(g => g.Group == group))
        {
            Groups.Insert(0, (0, group, false));
        }

        Accesses = calls is null
            ? []
            : TraceHashTableReader.GetAccesses(calls, Groups.Where(g => !g.IsWorktable).Select(g => g.Group).ToHashSet());

        EntryDetails = GetEntryDetails;

        Load();
    }

    public string Title => $"Hash Table 0x{(Iterator == 0 ? Group : Iterator):X16}";

    public string Key => $"HashTable:{(Iterator == 0 ? Group : Iterator):X}";

    public MemoryPageViewModel WorkfilePage { get; }

    public Func<ResultRow<long>, ResultDetails> EntryDetails { get; }

    public bool IsFieldTableVisible => FieldTable is not null;

    public bool IsEntryTableVisible => EntryTable is not null;

    private MemoryHistoryViewModel History { get; }

    private ulong Group { get; }

    private ulong Iterator { get; }

    private List<(int Slot, ulong Group, bool IsWorktable)> Groups { get; }

    private ITraceNavigator Navigator { get; }

    private TracePosition OpenedAt { get; }

    private TracePosition? Playhead { get; set; }

    private TracePosition Position => Playhead ?? OpenedAt;

    private PlayheadLoader<LoadedTable?> Loader { get; }

    private Dictionary<ulong, List<TraceHashAccess>> Accesses { get; }

    private LoadedTable? Loaded { get; set; }

    private string StructureKey { get; set; } = string.Empty;

    private string SelectedKey { get; set; }

    private Dictionary<ulong, MemoryHashTableNode> Nodes { get; set; } = [];

    private ulong HexAddress { get; set; }

    private ulong? SelectedItem { get; set; }

    private (ulong Group, int Partition, int Bucket)? PendingBucket { get; set; }

    private (ulong Group, TraceHashPart Part)? PendingPart { get; set; }

    private int? PendingSlotOffset { get; set; }

    private int PageBlock { get; set; } = -1;

    private string? MarkerKey { get; set; }

    private string? TableKey { get; set; }

    private List<(MemoryField Field, int Colour)> ShownFields { get; set; } = [];

    private int ShownBase { get; set; }

    private bool IsApplying { get; set; }

    private bool IsSelecting { get; set; }

    private Marker? MaskedMarker { get; set; }

    private List<HistoryEntry> NavigationHistory { get; } = [];

    private int HistoryIndex { get; set; } = -1;

    private bool IsReplaying { get; set; }

    private HistoryEntry CurrentEntry
        => new(SelectedKey,
               SelectedItem,
               Find(SelectedKey) is { Type: MemoryHashTableNodeType.WorkfilePage } ? WorkfilePage.SelectedSlot?.Offset : null);

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        Load();
    }

    public bool Contains(ulong group) => Groups.Exists(g => g.Group == group);

    public void ShowBucket(ulong group, int partition, int bucket)
    {
        PendingBucket = (group, partition, bucket);

        if (Loaded is not null)
        {
            SelectPendingBucket();
        }
    }

    public void ShowPart(ulong group, TraceHashPart part)
    {
        PendingPart = (group, part);

        if (Loaded is not null)
        {
            SelectPendingPart();
        }
    }

    public void SelectOffset(int offset)
    {
        if (SelectedNode is not { } node || Loaded is not { } loaded)
        {
            return;
        }

        var previous = MaskedMarker;

        if (node.Type is MemoryHashTableNodeType.HashTable
                      or MemoryHashTableNodeType.Group
                      or MemoryHashTableNodeType.WorktableGroup
                      or MemoryHashTableNodeType.Accessors
                      or MemoryHashTableNodeType.Partition)
        {
            var field = ShownFields.FindIndex(f => IsWithin((ulong)offset, (ulong)(ShownBase + f.Field.Offset), f.Field.Length));

            if (FieldTable?.Rows.FirstOrDefault(r => r.Id == field) is { } row)
            {
                SelectedFieldRow = row;
            }
        }
        else if (node.Type is MemoryHashTableNodeType.BucketPage or MemoryHashTableNodeType.OverflowPage
                 && GetPartition(loaded, node) is not null)
        {
            var size = (ulong)TraceHashTableLayout.BucketSize;

            var item = HexAddress + (ulong)(offset - loaded.HexBase) / size * size;

            if (EntryTable?.Rows.FirstOrDefault(r => r.Id == (long)item) is { } row)
            {
                SelectedEntryRow = row;
            }
        }
        else if (node.Type == MemoryHashTableNodeType.WorkfilePage)
        {
            WorkfilePage.SelectContaining(offset);
        }

        var marker = MarkerLookup.FindAt(Markers, offset);

        Mask(marker is not null && !IsSameMarker(marker, previous) ? marker : null);
    }

    public void Close()
    {
        Loader.Cancel();

        WorkfilePage.Clear();

        Loaded = null;

        Nodes = [];
    }

    partial void OnSelectedNodeChanged(MemoryHashTableNode? value)
    {
        if (value is { IsSelectable: true } && !IsApplying && !IsSelecting)
        {
            Select(value);
        }
    }

    partial void OnSelectedFieldRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id } || id < 0 || id >= ShownFields.Count)
        {
            return;
        }

        var field = ShownFields[(int)id].Field;

        Mask(Markers?.FirstOrDefault(m => m.StartPosition == ShownBase + field.Offset && m.Name == field.Name));
    }

    partial void OnSelectedEntryRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id })
        {
            return;
        }

        SelectedItem = (ulong)id;

        MaskItem();
    }

    private static LoadedTable? Read(MemoryHistoryViewModel history,
                                     ulong iterator,
                                     List<(int Slot, ulong Group, bool IsWorktable)> groups,
                                     Dictionary<ulong, List<TraceHashAccess>> accesses,
                                     TracePosition position,
                                     ulong hexAddress)
    {
        var store = history.Store;

        var decoded = new Dictionary<int, WorkfilePage?>();

        var rows = new Dictionary<ulong, List<(string Name, string Value)>>();

        var loaded = new List<LoadedGroup>();

        foreach (var (slot, group, isWorktable) in groups)
        {
            var table = isWorktable ? null : TraceHashTableReader.Read(store, group, position);

            loaded.Add(table is null
                ? new LoadedGroup(slot, group, isWorktable, null, null, null, [], [], null)
                : ReadGroup(history, slot, table, accesses.GetValueOrDefault(group) ?? [], decoded, rows, position));
        }

        if (loaded.TrueForAll(g => g.Table is null))
        {
            return null;
        }

        var hexBlock = hexAddress == 0 ? -1 : store.GetBlock(hexAddress, position);

        var count = hexBlock >= 0 ? store.GetWriteCount(hexBlock, position) : 0;

        var image = count > 0 ? store.GetImage(hexBlock, count - 1) : null;

        var data = image?.Data;

        var pointers = image is not null
            ? MemoryHistoryPointerReader.Find(store, history.BufferPool, hexBlock, image, count, position)
            : [];

        var hexBase = hexBlock >= 0 ? (int)(hexAddress - store.Blocks[hexBlock].Address) : 0;

        return new LoadedTable(iterator,
                               iterator == 0 ? 0 : TraceHashTableReader.GetCurrentGroup(store, iterator, position),
                               loaded,
                               rows,
                               hexAddress,
                               hexBlock,
                               hexBase,
                               data,
                               pointers);
    }

    private static LoadedGroup ReadGroup(MemoryHistoryViewModel history,
                                         int slot,
                                         TraceHashTable table,
                                         List<TraceHashAccess> accesses,
                                         Dictionary<int, WorkfilePage?> decoded,
                                         Dictionary<ulong, List<(string Name, string Value)>> rows,
                                         TracePosition position)
    {
        var store = history.Store;

        var span = CollectionsMarshal.AsSpan(accesses);

        var index = SortedSearch.FirstAfter<TraceHashAccess, TracePosition>(span, position, a => a.Position) - 1;

        TraceHashAccess? access = index >= 0 ? accesses[index] : null;

        var layout = GetLayout(history, table, slot);

        var workfiles = new List<List<PageEntry>>();

        foreach (var partition in table.Partitions)
        {
            var entries = new List<PageEntry>();

            foreach (var row in partition.Buckets.SelectMany(b => b.Entries).Select(e => e.Row))
            {
                var block = row == 0 ? -1 : store.GetBlock(row, position);

                if (block < 0 || !history.IsPage(block))
                {
                    continue;
                }

                var address = store.Blocks[block].Address;

                var found = entries.FindIndex(e => e.Address == address);

                if (found < 0)
                {
                    entries.Add(new PageEntry(address, 1));
                }
                else
                {
                    entries[found] = entries[found] with { Count = entries[found].Count + 1 };
                }

                if (ReadRow(history, decoded, block, row, position, layout) is { } values)
                {
                    rows[row] = values;
                }
            }

            entries.Sort((left, right) => left.Address.CompareTo(right.Address));

            workfiles.Add(entries);
        }

        var overflow = table.Partitions.Select(p => GetOverflowPages(history, p, position)).ToList();

        return new LoadedGroup(slot,
                               table.Group,
                               false,
                               table,
                               access,
                               access is { } value ? table.GetLocation(value.Hash) : null,
                               workfiles,
                               overflow,
                               layout);
    }

    private static RowRecordLayout? GetLayout(MemoryHistoryViewModel history, TraceHashTable table, int slot)
        => table.Columns.Count > 0
            ? history.GetWorkfileLayout(table.Iterator,
                                        [.. table.Columns.Select(c => c.Binding)],
                                        slot / TraceHashTableLayout.GroupsPerSide == 1)
            : null;

    private static List<(string Name, string Value)>? ReadRow(MemoryHistoryViewModel history,
                                                             Dictionary<int, WorkfilePage?> decoded,
                                                             int block,
                                                             ulong row,
                                                             TracePosition position,
                                                             RowRecordLayout? layout)
    {
        var store = history.Store;

        if (!decoded.TryGetValue(block, out var page))
        {
            var count = store.GetWriteCount(block, position);

            var data = count > 0 ? store.GetImage(block, count - 1).Data : null;

            page = data is not null ? WorkfilePageReader.Read(data, layout ?? history.GetWorkfileLayout(block)) : null;

            decoded[block] = page;
        }

        var offset = (int)(row - store.Blocks[block].Address);

        return page?.Records.FirstOrDefault(r => r.Offset == offset) is { } record
            ? [.. record.Fields.Select(f => (f.Name, f.Value))]
            : null;
    }

    private static ulong? ReadValue(byte[]? data, int offset, int length)
    {
        if (data is null || offset < 0 || offset + length > data.Length)
        {
            return null;
        }

        var span = data.AsSpan(offset, length);

        return length switch
        {
            sizeof(byte) => span[0],
            sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(span),
            _ => BinaryPrimitives.ReadUInt64LittleEndian(span)
        };
    }

    private static int GetField(IReadOnlyList<MemoryField> fields, int offset)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index].Offset == offset)
            {
                return index;
            }
        }

        return 0;
    }

    private static List<MemoryHashTableBadge> GetBadges(TraceHashTable table, TraceHashPartition partition)
    {
        var badges = new List<MemoryHashTableBadge>();

        if (partition.Address == table.ActivePartition)
        {
            badges.Add(new MemoryHashTableBadge(ActiveBadge, FieldColours[ActiveField].ToWindowsColor()));
        }

        if (partition.Index == 0)
        {
            badges.Add(new MemoryHashTableBadge(FirstBadge, FieldColours[FirstField].ToWindowsColor()));
        }

        if (partition.Index == table.Partitions.Count - 1)
        {
            badges.Add(new MemoryHashTableBadge(LastBadge, FieldColours[LastField].ToWindowsColor()));
        }

        return badges;
    }

    private static string GetBucketRange(TraceHashPartition partition, TraceHashPage page)
    {
        var last = Math.Min(page.FirstBucket + TraceHashTableLayout.BucketsPerPage, partition.BucketCount) - 1;

        return $"Buckets {page.FirstBucket}-{last}";
    }

    private static string GetRowText(int count) => count == 1 ? "1 Row" : $"{count} Rows";

    private static string GetItemText(int count) => count == 1 ? "1 Item" : $"{count} Items";

    private static List<PageEntry> GetOverflowPages(MemoryHistoryViewModel history, TraceHashPartition partition, TracePosition position)
    {
        var store = history.Store;

        var pages = new List<PageEntry>();

        foreach (var item in partition.Buckets.SelectMany(b => b.Items))
        {
            if (partition.Pages.Any(p => p.BucketPage != 0 && IsOnPage(item.Address, p.BucketPage))
                || store.GetBlock(item.Address, position) is not (>= 0 and var block))
            {
                continue;
            }

            var address = store.Blocks[block].Address;

            var found = pages.FindIndex(p => p.Address == address);

            if (found < 0)
            {
                pages.Add(new PageEntry(address, 1));
            }
            else
            {
                pages[found] = pages[found] with { Count = pages[found].Count + 1 };
            }
        }

        pages.Sort((left, right) => left.Address.CompareTo(right.Address));

        return pages;
    }

    private static string GetAddressText(ulong address) => $"0x{address:X16}";

    private static string GetText(long value) => value.ToString(CultureInfo.CurrentCulture);

    private static string GetGroupName(LoadedGroup group) => HashTablePartLabel.GetGroupName(group.Slot, group.IsWorktable);

    private static LoadedGroup? GetLoadedGroup(LoadedTable loaded, MemoryHashTableNode node)
        => loaded.Groups.Find(g => g.Address == node.Group);

    private static TraceHashPartition? GetPartition(LoadedTable loaded, MemoryHashTableNode node)
        => node.Partition >= 0 && GetLoadedGroup(loaded, node)?.Table is { } table && node.Partition < table.Partitions.Count
            ? table.Partitions[node.Partition]
            : null;

    private static IEnumerable<MemoryHashTableNode> Flatten(MemoryHashTableNode node)
        => node.Children.SelectMany(Flatten).Prepend(node);

    private static bool IsInRange(int bucket, int first) => bucket >= first && bucket < first + TraceHashTableLayout.BucketsPerPage;

    private static Marker CreateMarker(string name, int offset, int length, int colour)
        => new()
        {
            Name = name,
            StartPosition = offset,
            EndPosition = offset + length - 1,
            BackColour = FieldColours[colour].ToWindowsColor(),
            MapColour = FieldColours[colour].ToWindowsColor(),
            ForeColour = Colors.Black
        };

    private static object GetBucketCell(TraceHashAccess? found, int bucket, int current)
    {
        var text = GetText(bucket);

        if (bucket != current || found is not { } access)
        {
            return text;
        }

        var badge = access.IsInsert ? InsertBadge : access.IsMatch ? MatchBadge : NoMatchBadge;

        return new BadgeText([], text, [badge]);
    }

    private void Load()
    {
        var history = History;

        var iterator = Iterator;

        var groups = Groups;

        var accesses = Accesses;

        var position = Position;

        var hexAddress = HexAddress;

        Loader.Request(() => Read(history, iterator, groups, accesses, position, hexAddress));
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

        if (HistoryIndex < 0 && PendingPart is null && PendingBucket is null)
        {
            Remember(CurrentEntry);
        }

        if (PendingPart is not null)
        {
            SelectPendingPart();

            return;
        }

        if (PendingBucket is not null)
        {
            SelectPendingBucket();

            return;
        }

        Show();
    }

    private void BuildTree(LoadedTable loaded)
    {
        var root = new MemoryHashTableNode(MemoryHashTableNodeType.HashTable, HashTableName, loaded.Iterator, -1, null);

        foreach (var side in loaded.Groups.GroupBy(g => g.Slot / TraceHashTableLayout.GroupsPerSide).OrderBy(s => s.Key))
        {
            var name = HashTablePartLabel.GetSideName(side.First().Slot);

            var heading = side.Key == 0
                ? new MemoryHashTableNode(MemoryHashTableNodeType.BuildSide, name, 0, -1, root)
                : new MemoryHashTableNode(MemoryHashTableNodeType.ProbeSide, name, 0, -1, root);

            foreach (var group in side.OrderBy(g => g.Slot))
            {
                var type = group.IsWorktable ? MemoryHashTableNodeType.WorktableGroup : MemoryHashTableNodeType.Group;

                var node = new MemoryHashTableNode(type,
                                                   GetGroupName(group),
                                                   group.Address,
                                                   -1,
                                                   heading)
                {
                    IsActive = Accesses.TryGetValue(group.Address, out var used) && used.Count > 0,
                    Badges = group.Address == loaded.CurrentGroup
                        ? [new MemoryHashTableBadge(InUseBadge, FieldColours[CurrentField].ToWindowsColor())]
                        : []
                };

                if (group.Table is { Columns.Count: > 0 } table)
                {
                    node.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.Accessors, AccessorsName, table.ColumnArray, -1, node)
                    {
                        Detail = table.Columns.Count == 1 ? "1 Column" : $"{table.Columns.Count} Columns"
                    });
                }

                foreach (var partition in group.Table?.Partitions ?? [])
                {
                    node.Children.Add(CreatePartition(group, partition, node));
                }

                heading.Children.Add(node);
            }

            root.Children.Add(heading);
        }

        var key = string.Join("|", Flatten(root).Select(n => $"{n.Key} {n.Detail} {string.Join(",", n.Badges)}"));

        if (key == StructureKey)
        {
            return;
        }

        StructureKey = key;

        var nodes = new Dictionary<ulong, MemoryHashTableNode>();

        foreach (var node in Flatten(root).Where(n => n.IsSelectable))
        {
            nodes.TryAdd(node.Address, node);

            if (node.Type == MemoryHashTableNodeType.Group)
            {
                nodes.TryAdd(node.Address + TraceHashTableLayout.PartitionListOffset, node);
            }

            if (node.Type == MemoryHashTableNodeType.Partition)
            {
                nodes.TryAdd(node.Address + TraceHashTableLayout.PartitionLinkOffset, node);
            }
        }

        Nodes = nodes;

        Roots = [root];

        SelectedNode = Find(SelectedKey) ?? Flatten(root).FirstOrDefault(n => n.Type == MemoryHashTableNodeType.Group) ?? root;

        SelectedKey = SelectedNode.Key;
    }

    private MemoryHashTableNode CreatePartition(LoadedGroup group, TraceHashPartition partition, MemoryHashTableNode parent)
    {
        var node = new MemoryHashTableNode(MemoryHashTableNodeType.Partition,
                                           $"Partition {partition.Index}",
                                           partition.Address,
                                           partition.Index,
                                           parent)
        {
            Badges = GetBadges(group.Table!, partition)
        };

        List<PageEntry> overflow = partition.Index < group.OverflowPages.Count ? group.OverflowPages[partition.Index] : [];

        if (partition.Pages.Any(p => p.BucketPage != 0) || overflow.Count > 0)
        {
            var heading = new MemoryHashTableNode(MemoryHashTableNodeType.BucketPages, BucketPagesName, 0, partition.Index, node);

            foreach (var page in partition.Pages.Where(p => p.BucketPage != 0).OrderBy(p => p.FirstBucket))
            {
                heading.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.BucketPage,
                                                             BucketPageName,
                                                             page.BucketPage,
                                                             partition.Index,
                                                             heading)
                {
                    FirstBucket = page.FirstBucket,
                    Detail = GetBucketRange(partition, page)
                });
            }

            foreach (var page in overflow)
            {
                heading.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.OverflowPage,
                                                             OverflowPageName,
                                                             page.Address,
                                                             partition.Index,
                                                             heading)
                {
                    Detail = GetItemText(page.Count)
                });
            }

            node.Children.Add(heading);
        }

        if (partition.Index < group.Workfiles.Count && group.Workfiles[partition.Index] is { Count: > 0 } workfiles)
        {
            var heading = new MemoryHashTableNode(MemoryHashTableNodeType.WorkfilePages, WorkfilePagesName, 0, partition.Index, node);

            foreach (var page in workfiles)
            {
                heading.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.WorkfilePage,
                                                             WorkfilePageName,
                                                             page.Address,
                                                             partition.Index,
                                                             heading)
                {
                    Detail = GetRowText(page.Count)
                });
            }

            node.Children.Add(heading);
        }

        return node;
    }

    private MemoryHashTableNode? Find(string key) => Roots.SelectMany(Flatten).FirstOrDefault(n => n.Key == key);

    private void Select(MemoryHashTableNode node, ulong? item = null)
    {
        var target = Find(node.Key) ?? node;

        Remember(new HistoryEntry(target.Key, item ?? (target.Key == SelectedKey ? SelectedItem : null), PendingSlotOffset));

        if (target.Key != SelectedKey || IsReplaying)
        {
            SelectedKey = target.Key;

            SelectedItem = null;

            SelectedFieldRow = null;

            SelectedEntryRow = null;

            Mask(null);
        }

        if (item is not null)
        {
            SelectedItem = item;
        }

        IsSelecting = true;

        SelectedNode = target;

        IsSelecting = false;

        Show();
    }

    private void SelectPendingPart()
    {
        if (PendingPart is not var (group, part))
        {
            return;
        }

        PendingPart = null;

        if (GetPointerNode(part.Address) is { } node)
        {
            OpenPointer(node, part.Address);

            return;
        }

        var partition = Roots.SelectMany(Flatten)
                             .FirstOrDefault(n => n.Type == MemoryHashTableNodeType.Partition
                                                  && n.Group == group
                                                  && n.Partition == part.Partition);

        if (partition is null)
        {
            Show();

            return;
        }

        Select(partition);
    }

    private MemoryHashTableNode? GetPointerNode(ulong value)
        => Nodes.TryGetValue(value, out var node)
            ? node
            : Roots.SelectMany(Flatten)
                   .FirstOrDefault(n => n.Type is MemoryHashTableNodeType.BucketPage
                                               or MemoryHashTableNodeType.OverflowPage
                                               or MemoryHashTableNodeType.WorkfilePage
                                        && IsOnPage(value, n.Address));

    private void OpenPointer(MemoryHashTableNode node, ulong value)
    {
        if (node.Type == MemoryHashTableNodeType.WorkfilePage)
        {
            OpenRow(value);

            return;
        }

        Select(node);
    }

    private List<HexLink> GetLinks(LoadedTable loaded)
    {
        var links = new List<HexLink>();

        var position = Position;

        var size = MemoryHistoryPointerReader.PointerSize;

        foreach (var (offset, value, block, page) in loaded.Pointers)
        {
            if (GetPointerNode(value) is { } node)
            {
                links.Add(new HexLink(offset, size, PointerPrefix + node.Name, SelectCommand, () => OpenPointer(node, value)));
            }
            else if (block >= 0)
            {
                var target = History.GetPointerTarget(value, position) ?? GetAddressText(value);

                links.Add(new HexLink(offset,
                                      size,
                                      PointerPrefix + target,
                                      OpenMemoryCommand,
                                      () => Navigator.OpenMemory(block, value, position)));
            }
            else if (page is { } reference && History.IsQueryDatabase(reference))
            {
                var label = History.GetBufferPageLabel(reference);

                links.Add(new HexLink(offset, size, PointerPrefix + label, OpenPageCommand, () => Navigator.OpenBufferPage(reference)));
            }
        }

        return links;
    }

    private void SelectPendingBucket()
    {
        if (PendingBucket is not { } pending)
        {
            return;
        }

        PendingBucket = null;

        var (group, partition, bucket) = pending;

        var nodes = Roots.SelectMany(Flatten).Where(n => n.Group == group && n.Partition == partition).ToList();

        var node = nodes.FirstOrDefault(n => n.Type == MemoryHashTableNodeType.BucketPage && IsInRange(bucket, n.FirstBucket))
                   ?? nodes.FirstOrDefault(n => n.Type == MemoryHashTableNodeType.Partition);

        if (node is null)
        {
            Show();

            return;
        }

        if (node.Type != MemoryHashTableNodeType.BucketPage)
        {
            Select(node);

            return;
        }

        Select(node, node.Address + (ulong)((bucket - node.FirstBucket) * TraceHashTableLayout.BucketSize));
    }

    private void Show()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        HexAddress = node.IsSelectable ? node.Address : 0;

        ShowPath(node);

        if (Loaded is { } loaded && loaded.HexAddress == HexAddress)
        {
            ShowDetail(loaded, node);
        }
        else
        {
            Load();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => Travel(-1);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() => Travel(1);

    private void Remember(HistoryEntry entry)
    {
        if (IsReplaying)
        {
            return;
        }

        if (HistoryIndex >= 0)
        {
            NavigationHistory[HistoryIndex] = CurrentEntry;

            if (NavigationHistory[HistoryIndex] == entry)
            {
                return;
            }

            NavigationHistory.RemoveRange(HistoryIndex + 1, NavigationHistory.Count - HistoryIndex - 1);
        }

        NavigationHistory.Add(entry);

        HistoryIndex = NavigationHistory.Count - 1;

        UpdateHistory();
    }

    private void Travel(int step)
    {
        if (HistoryIndex < 0)
        {
            return;
        }

        NavigationHistory[HistoryIndex] = CurrentEntry;

        for (var index = HistoryIndex + step; index >= 0 && index < NavigationHistory.Count; index += step)
        {
            var entry = NavigationHistory[index];

            if (Find(entry.Key) is not { } node)
            {
                continue;
            }

            HistoryIndex = index;

            IsReplaying = true;

            PendingSlotOffset = entry.SlotOffset;

            Select(node, entry.Item);

            IsReplaying = false;

            break;
        }

        UpdateHistory();
    }

    private void UpdateHistory()
    {
        CanGoBack = HistoryIndex > 0;

        CanGoForward = HistoryIndex >= 0 && HistoryIndex < NavigationHistory.Count - 1;
    }

    private void ShowPath(MemoryHashTableNode node)
    {
        var chain = new List<MemoryHashTableNode>();

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.IsSelectable)
            {
                chain.Insert(0, current);
            }
        }

        Path = [.. chain.Select(n => new ActionLink(n.Name, () => Select(n)))];

        PathTitle = node.Name;
    }

    private void ShowDetail(LoadedTable loaded, MemoryHashTableNode node)
    {
        if (Data is null || loaded.Data is null || !loaded.Data.AsSpan().SequenceEqual(Data))
        {
            Data = loaded.Data;
        }

        Links = GetLinks(loaded);

        EmptyText = node.IsSelectable && loaded.Data is null ? NotAllocated : string.Empty;

        switch (node.Type)
        {
            case MemoryHashTableNodeType.HashTable:
                ShowFields(loaded, node, TraceHashTableLayout.IteratorFields);
                break;

            case MemoryHashTableNodeType.Group:
                ShowFields(loaded, node, TraceHashTableLayout.GroupFields);
                break;

            case MemoryHashTableNodeType.WorktableGroup:
                ShowFields(loaded, node, []);
                break;

            case MemoryHashTableNodeType.Accessors:
                ShowAccessors(loaded, node);
                break;

            case MemoryHashTableNodeType.Partition:
                ShowFields(loaded, node, TraceHashTableLayout.PartitionFields);
                break;

            case MemoryHashTableNodeType.BucketPage:
            case MemoryHashTableNodeType.OverflowPage:
                ShowEntries(loaded, node);
                break;

            case MemoryHashTableNodeType.WorkfilePage:
                ShowPage(loaded, node);
                break;

            default:
                ClearDetail();
                break;
        }
    }

    private void ClearDetail()
    {
        Data = null;

        Markers = null;

        MarkerKey = null;

        TableKey = null;

        FieldTable = null;

        EntryTable = null;

        IsPageVisible = false;

        ShownFields = [];
    }

    private void ShowFields(LoadedTable loaded, MemoryHashTableNode node, IReadOnlyList<MemoryField> layout)
    {
        EntryTable = null;

        IsPageVisible = false;

        ShownBase = loaded.HexBase;

        ShownFields = [.. layout.Select((f, i) => (f, i))];

        SetMarkers(node.Key, () => ShownFields.Select(f => CreateMarker(f.Field.Name,
                                                                        ShownBase + f.Field.Offset,
                                                                        f.Field.Length,
                                                                        f.Colour)));

        var values = ShownFields.Select(f => ReadValue(loaded.Data, ShownBase + f.Field.Offset, f.Field.Length)).ToList();

        var key = $"{node.Key} {StructureKey.GetHashCode()} {string.Join(",", values)}";

        if (key == TableKey && FieldTable is not null)
        {
            return;
        }

        TableKey = key;

        var selected = SelectedFieldRow?.Id;

        var columns = new List<ResultColumn>
        {
            new(0, "Field", typeof(string), false) { Width = FieldWidth },
            new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(2, "Value", typeof(string), true) { Width = ValueWidth }
        };

        var rows = ShownFields.Select((f, i) => new ResultRow<long>([new SwatchText(f.Field.Name, FieldColours[f.Colour]),
                                                                     $"0x{f.Field.Offset:X}",
                                                                     GetValueCell(f.Field, values[i])])
        {
            Id = i
        }).ToList();

        FieldTable = new QueryResultSet { Columns = columns, Rows = rows };

        SelectedFieldRow = rows.FirstOrDefault(r => r.Id == selected);
    }

    private void ShowAccessors(LoadedTable loaded, MemoryHashTableNode node)
    {
        EntryTable = null;

        IsPageVisible = false;

        if (GetLoadedGroup(loaded, node) is not { Table: { } table } group)
        {
            FieldTable = null;

            ShownFields = [];

            return;
        }

        var names = group.Layout?.Structure.Columns;

        ShownBase = loaded.HexBase;

        ShownFields =
        [
            .. table.Columns.Select((c, i) => (new MemoryField(GetColumnName(names, c, i), i * sizeof(ulong), sizeof(ulong)),
                                               i % FieldColours.Count))
        ];

        SetMarkers(node.Key, () => ShownFields.Select(f => CreateMarker(f.Field.Name,
                                                                        ShownBase + f.Field.Offset,
                                                                        f.Field.Length,
                                                                        f.Colour)));

        var key = $"{node.Key} {StructureKey.GetHashCode()}";

        if (key == TableKey && FieldTable is not null)
        {
            return;
        }

        TableKey = key;

        var selected = SelectedFieldRow?.Id;

        var columns = new List<ResultColumn>
        {
            new(0, "Column", typeof(string), false) { Width = FieldWidth },
            new(1, "Type", typeof(string), false) { Width = TypeWidth },
            new(2, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(3, "Value", typeof(string), false) { Width = AccessorWidth },
            new(4, "Scan Value", typeof(string), false) { Width = AccessorWidth },
            new(5, "Bindings", typeof(string), true) { Width = AccessorWidth }
        };

        var rows = table.Columns.Select((c, i) => new ResultRow<long>(
        [
            new SwatchText(ShownFields[i].Field.Name, FieldColours[ShownFields[i].Colour]),
            ColumnBindingText.GetType(c.Binding),
            ColumnBindingText.GetOffset(c.Binding),
            GetPointerCell(c.Value),
            GetPointerCell(c.ScanValue),
            GetPointerCell(c.Bindings)
        ])
        {
            Id = i
        }).ToList();

        FieldTable = new QueryResultSet { Columns = columns, Rows = rows };

        SelectedFieldRow = rows.FirstOrDefault(r => r.Id == selected);
    }

    private static string GetColumnName(List<ColumnStructure>? names, TraceHashColumn column, int index)
        => names is not null && index < names.Count ? names[index].ColumnName : $"Column {column.Binding.Ordinal}";

    private object? GetValueCell(MemoryField field, ulong? value)
    {
        if (value is not { } found)
        {
            return null;
        }

        if (field.IsPointer)
        {
            return GetPointerCell(found);
        }

        return field.IsHex ? $"0x{found:X8}" : found.ToString("N0", CultureInfo.CurrentCulture);
    }

    private object GetPointerCell(ulong pointer)
    {
        var text = $"0x{pointer:X16}";

        if (pointer == 0)
        {
            return text;
        }

        if (Nodes.TryGetValue(pointer, out var node))
        {
            return new ActionLinks([new ActionLink(text, () => Select(node))]) { Suffix = node.Name };
        }

        var position = Position;

        var block = History.GetBlock(pointer, position);

        if (block < 0)
        {
            return text;
        }

        return new ActionLinks([new ActionLink(text, () => Navigator.OpenMemory(block, pointer, position))])
        {
            Suffix = History.GetPointerTarget(pointer, position)
        };
    }

    private void ShowEntries(LoadedTable loaded, MemoryHashTableNode node)
    {
        FieldTable = null;

        IsPageVisible = false;

        ShownFields = [];

        if (GetLoadedGroup(loaded, node) is not { } group || GetPartition(loaded, node) is not { } partition)
        {
            EntryTable = null;

            return;
        }

        var page = node.Address;

        var hexBase = loaded.HexBase;

        var content = string.Join(",", partition.Buckets.Select(GetBucketKey));

        SetMarkers($"{node.Key} {content}", () => GetPageMarkers(partition, page, hexBase));

        var current = group.Location is { } location && location.Partition == node.Partition ? location.Bucket : -1;

        var key = $"{node.Key} {current} {content}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = node.Type == MemoryHashTableNodeType.BucketPage
                ? CreateBucketPageTable(group.Access, partition, node, current)
                : CreateOverflowPageTable(group.Access, partition, page, current);
        }

        if (SelectedItem is { } selected && EntryTable.Rows.FirstOrDefault(r => r.Id == (long)selected) is { } row)
        {
            if (!ReferenceEquals(row, SelectedEntryRow))
            {
                SelectedEntryRow = row;
            }
        }
    }

    private static IEnumerable<Marker> GetPageMarkers(TraceHashPartition partition, ulong page, int hexBase)
    {
        var fields = TraceHashTableLayout.ItemFields;

        foreach (var bucket in partition.Buckets)
        {
            if (IsOnPage(bucket.Address, page))
            {
                var head = fields[HeadField];

                yield return CreateMarker($"Bucket {bucket.Index} {head.Name}",
                                          hexBase + (int)(bucket.Address - page) + head.Offset,
                                          head.Length,
                                          HeadField);
            }

            foreach (var item in bucket.Items.Where(i => IsOnPage(i.Address, page)))
            {
                var offset = hexBase + (int)(item.Address - page);

                foreach (var field in new[] { CountField, NextField })
                {
                    yield return CreateMarker($"Bucket {bucket.Index} {fields[field].Name}",
                                              offset + fields[field].Offset,
                                              fields[field].Length,
                                              field);
                }

                for (var entry = 0; entry < item.Entries.Count; entry++)
                {
                    yield return CreateMarker($"Bucket {bucket.Index} {fields[KeyField].Name} {entry + 1}",
                                              offset + TraceHashTableLayout.GetKeyOffset(entry),
                                              fields[KeyField].Length,
                                              KeyField);

                    yield return CreateMarker($"Bucket {bucket.Index} {fields[RowField].Name} {entry + 1}",
                                              offset + TraceHashTableLayout.GetRowOffset(entry),
                                              fields[RowField].Length,
                                              RowField);
                }
            }
        }
    }

    private static bool IsOnPage(ulong address, ulong page) => address >= page && address < page + PageSize;

    private static bool IsWithin(ulong address, ulong start, int length) => address >= start && address < start + (ulong)length;

    private static string GetBucketKey(TraceHashBucket bucket)
        => $"{bucket.Index}:{string.Join(";", bucket.Items.Select(i => $"{i.Address:X}/{i.Entries.Count}"))}";

    private static Dictionary<ulong, (TraceHashBucket Bucket, int Position)> GetItemOwners(TraceHashPartition partition)
    {
        var owners = new Dictionary<ulong, (TraceHashBucket Bucket, int Position)>();

        foreach (var bucket in partition.Buckets)
        {
            for (var position = 0; position < bucket.Items.Count; position++)
            {
                owners.TryAdd(bucket.Items[position].Address, (bucket, position));
            }
        }

        return owners;
    }

    private static ulong GetNext(TraceHashBucket bucket, int position)
        => position + 1 < bucket.Items.Count ? bucket.Items[position + 1].Address : 0;

    private QueryResultSet CreateBucketPageTable(TraceHashAccess? access,
                                                 TraceHashPartition partition,
                                                 MemoryHashTableNode node,
                                                 int current)
    {
        var fields = TraceHashTableLayout.ItemFields;

        var columns = new List<ResultColumn>
        {
            new(0, "Bucket", typeof(string), false) { Width = BucketWidth },
            new(1, fields[HeadField].Name, typeof(string), false) { Width = RowWidth, MarkerColour = FieldColours[HeadField] },
            new(2, fields[NextField].Name, typeof(string), false) { Width = RowWidth, MarkerColour = FieldColours[NextField] },
            new(3, fields[CountField].Name, typeof(string), false) { Width = RowCountWidth, MarkerColour = FieldColours[CountField] }
        };

        var owners = GetItemOwners(partition);

        var heads = partition.Buckets.ToDictionary(b => b.Address);

        var last = Math.Min(node.FirstBucket + TraceHashTableLayout.BucketsPerPage, partition.BucketCount);

        var rows = new List<ResultRow<long>>();

        for (var bucket = node.FirstBucket; bucket < last; bucket++)
        {
            var slot = node.Address + (ulong)((bucket - node.FirstBucket) * TraceHashTableLayout.BucketSize);

            var hasHead = heads.TryGetValue(slot, out var head);

            var hasItem = owners.TryGetValue(slot, out var item);

            if (!hasHead && !hasItem && bucket != current)
            {
                continue;
            }

            object?[] cells =
            [
                GetBucketCell(access, bucket, current),
                hasHead ? GetItemCell(head!.Items[0].Address, slot) : null,
                hasItem ? GetItemCell(GetNext(item.Bucket, item.Position), 0) : null,
                GetText(hasItem ? item.Bucket.Items[item.Position].Entries.Count : 0)
            ];

            rows.Add(new ResultRow<long>(cells) { Id = (long)slot });
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private QueryResultSet CreateOverflowPageTable(TraceHashAccess? access, TraceHashPartition partition, ulong page, int current)
    {
        var fields = TraceHashTableLayout.ItemFields;

        var columns = new List<ResultColumn>
        {
            new(0, "Bucket", typeof(string), false) { Width = BucketWidth },
            new(1, fields[NextField].Name, typeof(string), false) { Width = RowWidth, MarkerColour = FieldColours[NextField] },
            new(2, fields[CountField].Name, typeof(string), false) { Width = RowCountWidth, MarkerColour = FieldColours[CountField] }
        };

        var rows = new List<ResultRow<long>>();

        foreach (var (address, (bucket, position)) in GetItemOwners(partition).Where(o => IsOnPage(o.Key, page)).OrderBy(o => o.Key))
        {
            object?[] cells =
            [
                GetBucketCell(access, bucket.Index, current),
                GetItemCell(GetNext(bucket, position), 0),
                GetText(bucket.Items[position].Entries.Count)
            ];

            rows.Add(new ResultRow<long>(cells) { Id = (long)address });
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private object? GetItemCell(ulong address, ulong self)
    {
        if (address == 0)
        {
            return null;
        }

        var text = GetAddressText(address);

        return address == self ? text : new ActionLinks([new ActionLink(text, () => JumpToItem(address))]);
    }

    private void JumpToItem(ulong address)
    {
        if (GetPointerNode(address) is { } node)
        {
            Select(node, address);
        }
    }

    private ActionLinks GetRowCell(ulong row)
        => new([new ActionLink(GetAddressText(row), () => OpenRow(row))]) { Badges = [GetRowTarget(row)] };

    private string GetRowTarget(ulong row)
    {
        var position = Position;

        var block = History.GetBlock(row, position);

        if (block >= 0)
        {
            return History.IsPage(block) ? WorkfileBadge : History.GetTypeName(block);
        }

        return History.GetBufferPage(row, position) is { IsBuffer: false } ? BufferPoolBadge : UnknownBadge;
    }

    private ResultDetails GetEntryDetails(ResultRow<long> row)
    {
        if (Loaded is not { } loaded
            || SelectedNode is not { } node
            || GetPartition(loaded, node) is not { } partition
            || !GetItemOwners(partition).TryGetValue((ulong)row.Id, out var owner))
        {
            return new ResultDetails(string.Empty, [], NoRows);
        }

        var fields = TraceHashTableLayout.ItemFields;

        var item = owner.Bucket.Items[owner.Position];

        var values = item.Entries.Select(e => loaded.Rows.GetValueOrDefault(e.Row)).FirstOrDefault(r => r is not null) ?? [];

        var columns = new List<ResultColumn>
        {
            new(0, "Entry", typeof(string), false) { Width = EntryWidth, Alignment = ResultAlignment.Right },
            new(1, fields[KeyField].Name, typeof(string), false) { Width = KeyWidth, MarkerColour = FieldColours[KeyField] },
            new(2, fields[RowField].Name, typeof(string), false) { Width = RowWidth, MarkerColour = FieldColours[RowField] }
        };

        columns.AddRange(values.Select((v, i) => new ResultColumn(FixedEntryColumns + i, v.Name, typeof(string), true)
        {
            Width = ColumnWidth
        }));

        var rows = new List<ResultRow<long>>();

        for (var index = 0; index < item.Entries.Count; index++)
        {
            var entry = item.Entries[index];

            var cells = loaded.Rows.GetValueOrDefault(entry.Row) ?? [];

            var address = entry.Row;

            rows.Add(new ResultRow<long>(
            [
                GetText(index + 1),
                GetText(entry.Key),
                address == 0 ? null : GetRowCell(address),
                .. values.Select(v => (object?)cells.FirstOrDefault(c => c.Name == v.Name).Value)
            ])
            {
                Id = index
            });
        }

        return new ResultDetails(string.Empty, []) { ResultSet = new QueryResultSet { Columns = columns, Rows = rows } };
    }

    private void MaskItem()
    {
        if (SelectedItem is not { } item || Loaded is not { } loaded || !IsOnPage(item, HexAddress))
        {
            return;
        }

        var offset = loaded.HexBase + (int)(item - HexAddress);

        Mask(new Marker
        {
            Name = GetAddressText(item),
            StartPosition = offset,
            EndPosition = offset + TraceHashTableLayout.BucketSize - 1
        });
    }

    private void Mask(Marker? marker)
    {
        MaskedMarker = marker;

        SelectedMarker = marker;
    }

    private static bool IsSameMarker(Marker marker, Marker? other)
        => other is not null
           && marker.StartPosition == other.StartPosition
           && marker.EndPosition == other.EndPosition
           && marker.Name == other.Name;

    private void OpenRow(ulong row)
    {
        var page = Roots.SelectMany(Flatten)
                        .FirstOrDefault(n => n.Type == MemoryHashTableNodeType.WorkfilePage
                                             && row >= n.Address
                                             && row < n.Address + PageSize);

        if (page is not null)
        {
            PendingSlotOffset = (int)(row - page.Address);

            Select(page);

            return;
        }

        var position = Position;

        var block = History.GetBlock(row, position);

        if (block >= 0)
        {
            Navigator.OpenMemory(block, row, position);
        }
        else if (History.GetBufferPage(row, position) is { IsBuffer: false } reference && History.IsQueryDatabase(reference))
        {
            Navigator.OpenBufferPage(reference);
        }
    }

    private void ShowPage(LoadedTable loaded, MemoryHashTableNode node)
    {
        FieldTable = null;

        EntryTable = null;

        TableKey = null;

        ShownFields = [];

        IsPageVisible = true;

        if (loaded.HexBlock != PageBlock)
        {
            PageBlock = loaded.HexBlock;

            if (PageBlock >= 0)
            {
                WorkfilePage.Load(History, PageBlock, GetLoadedGroup(loaded, node)?.Layout);
            }
            else
            {
                WorkfilePage.Clear();
            }
        }

        WorkfilePage.SetData(Data);

        WorkfilePage.Refresh();

        if (PendingSlotOffset is { } offset)
        {
            PendingSlotOffset = null;

            WorkfilePage.SelectOffset(offset);
        }

        MarkerKey = null;

        Markers = WorkfilePage.Markers;
    }

    private void ShowPageMarkers(int? offset)
    {
        if (SelectedNode is not { Type: MemoryHashTableNodeType.WorkfilePage })
        {
            return;
        }

        Markers = WorkfilePage.Markers;

        if (offset is { } scroll)
        {
            ScrollToOffset = scroll;
        }
    }

    private void SetMarkers(string key, Func<IEnumerable<Marker>> create)
    {
        if (key == MarkerKey && Markers is not null)
        {
            return;
        }

        MarkerKey = key;

        Markers = new ObservableCollection<Marker>(create());
    }

    private readonly record struct PageEntry(ulong Address, int Count);

    private readonly record struct HistoryEntry(string Key, ulong? Item, int? SlotOffset);

    private sealed record LoadedGroup(int Slot,
                                      ulong Address,
                                      bool IsWorktable,
                                      TraceHashTable? Table,
                                      TraceHashAccess? Access,
                                      (int Partition, int Bucket)? Location,
                                      List<List<PageEntry>> Workfiles,
                                      List<List<PageEntry>> OverflowPages,
                                      RowRecordLayout? Layout);

    private sealed record LoadedTable(ulong Iterator,
                                      ulong CurrentGroup,
                                      List<LoadedGroup> Groups,
                                      Dictionary<ulong, List<(string Name, string Value)>> Rows,
                                      ulong HexAddress,
                                      int HexBlock,
                                      int HexBase,
                                      byte[]? Data,
                                      List<MemoryHistoryPointer> Pointers);
}

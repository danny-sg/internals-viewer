using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHashTableViewModel : ObservableObject
{
    private const string GroupName = "Partition Group";

    private const string PartitionsName = "Partition Instances";

    private const string DirectoryName = "Directory";

    private const string BucketPageName = "Item Page";

    private const string BucketPagesName = "Hash Bucket Pages";

    private const string OverflowPageName = "Overflow Page";

    private const string WorkfilePagesName = "Workfile Pages";

    private const string WorkfilePageName = "Workfile Page";

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

    private const int BucketWidth = 130;

    private const int RowCountWidth = 90;

    private const int KeyWidth = 100;

    private const int RowWidth = 240;

    private const int ColumnWidth = 120;

    private const int EntryWidth = 70;

    private const int PageSize = 8192;

    private const int FixedEntryColumns = 3;

    private const int DirectoryShift = 16;

    private const int ActiveField = 1;

    private const int LastField = 2;

    private const int FirstField = 3;

    private const int CountField = 0;

    private const int HeadField = 1;

    private const int NextField = 2;

    private const int KeyField = 3;

    private const int RowField = 4;

    private static readonly TimeSpan LoadInterval = TimeSpan.FromMilliseconds(50);

    private static readonly int ColourCount = new[]
    {
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
    private LogRecordAnnotation? _selectedChangeSpan;

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

    public MemoryHashTableViewModel(MemoryHistoryViewModel history, ulong group, TracePosition position, ITraceNavigator navigator)
    {
        History = history;

        Group = group;

        OpenedAt = position;

        Navigator = navigator;

        SelectedKey = new MemoryHashTableNode(MemoryHashTableNodeType.Group, GroupName, group, -1, null).Key;

        WorkfilePage = new MemoryPageViewModel(ShowPageMarkers);

        Loader = new PlayheadLoader<LoadedTable?>(LoadInterval, Apply);

        Accesses = history.CallStore is { } calls ? TraceHashTableReader.GetAccesses(calls, group) : [];

        EntryDetails = GetEntryDetails;

        Load();
    }

    public string Title => $"Hash Table 0x{Group:X16}";

    public MemoryPageViewModel WorkfilePage { get; }

    public Func<ResultRow<long>, ResultDetails> EntryDetails { get; }

    public bool IsFieldTableVisible => FieldTable is not null;

    public bool IsEntryTableVisible => EntryTable is not null;

    private MemoryHistoryViewModel History { get; }

    private ulong Group { get; }

    private ITraceNavigator Navigator { get; }

    private TracePosition OpenedAt { get; }

    private TracePosition? Playhead { get; set; }

    private TracePosition Position => Playhead ?? OpenedAt;

    private PlayheadLoader<LoadedTable?> Loader { get; }

    private List<TraceHashAccess> Accesses { get; }

    private LoadedTable? Loaded { get; set; }

    private string StructureKey { get; set; } = string.Empty;

    private string SelectedKey { get; set; }

    private Dictionary<ulong, MemoryHashTableNode> Nodes { get; set; } = [];

    private ulong HexAddress { get; set; }

    private ulong? SelectedItem { get; set; }

    private (int Partition, int Bucket)? PendingBucket { get; set; }

    private TraceHashPart? PendingPart { get; set; }

    private int? PendingSlotOffset { get; set; }

    private int PageBlock { get; set; } = -1;

    private string? MarkerKey { get; set; }

    private string? TableKey { get; set; }

    private List<(MemoryField Field, int Colour)> ShownFields { get; set; } = [];

    private int ShownBase { get; set; }

    private bool IsApplying { get; set; }

    private bool IsSelecting { get; set; }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        Load();
    }

    public void ShowBucket(int partition, int bucket)
    {
        PendingBucket = (partition, bucket);

        if (Loaded is not null)
        {
            SelectPendingBucket();
        }
    }

    public void ShowPart(TraceHashPart part)
    {
        PendingPart = part;

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

        if (node.Type is MemoryHashTableNodeType.Group or MemoryHashTableNodeType.Partition)
        {
            var field = ShownFields.FindIndex(f => IsWithin((ulong)offset, (ulong)(ShownBase + f.Field.Offset), f.Field.Length));

            if (FieldTable?.Rows.FirstOrDefault(r => r.Id == field) is { } row)
            {
                SelectedFieldRow = row;
            }
        }
        else if (node.Type is MemoryHashTableNodeType.BucketPage or MemoryHashTableNodeType.OverflowPage
                 && node.Partition < loaded.Table.Partitions.Count)
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

        OutlineMarker(offset);
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

        var (field, colour) = ShownFields[(int)id];

        Outline(ShownBase + field.Offset, field.Length, field.Name, FieldColours[colour]);
    }

    partial void OnSelectedEntryRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id })
        {
            return;
        }

        SelectedItem = (ulong)id;

        OutlineItem();
    }

    private static LoadedTable? Read(MemoryHistoryViewModel history,
                                     ulong group,
                                     List<TraceHashAccess> accesses,
                                     TracePosition position,
                                     ulong hexAddress)
    {
        var store = history.Store;

        if (TraceHashTableReader.Read(store, group, position) is not { } table)
        {
            return null;
        }

        var span = CollectionsMarshal.AsSpan(accesses);

        var index = SortedSearch.FirstAfter<TraceHashAccess, TracePosition>(span, position, a => a.Position) - 1;

        TraceHashAccess? access = index >= 0 ? accesses[index] : null;

        var decoded = new Dictionary<int, WorkfilePage?>();

        var rows = new Dictionary<ulong, List<(string Name, string Value)>>();

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

                if (ReadRow(history, decoded, block, row, position) is { } values)
                {
                    rows[row] = values;
                }
            }

            entries.Sort((left, right) => left.Address.CompareTo(right.Address));

            workfiles.Add(entries);
        }

        var overflow = table.Partitions.Select(p => GetOverflowPages(history, p, position)).ToList();

        var hexBlock = hexAddress == 0 ? -1 : store.GetBlock(hexAddress, position);

        var count = hexBlock >= 0 ? store.GetWriteCount(hexBlock, position) : 0;

        var image = count > 0 ? store.GetImage(hexBlock, count - 1) : null;

        var data = image?.Data;

        var pointers = image is not null
            ? MemoryHistoryPointerReader.Find(store, history.BufferPool, hexBlock, image, count, position)
            : [];

        var hexBase = hexBlock >= 0 ? (int)(hexAddress - store.Blocks[hexBlock].Address) : 0;

        return new LoadedTable(table,
                               access,
                               access is { } value ? table.GetLocation(value.Hash) : null,
                               workfiles,
                               overflow,
                               rows,
                               hexAddress,
                               hexBlock,
                               hexBase,
                               data,
                               pointers);
    }

    private static List<(string Name, string Value)>? ReadRow(MemoryHistoryViewModel history,
                                                             Dictionary<int, WorkfilePage?> decoded,
                                                             int block,
                                                             ulong row,
                                                             TracePosition position)
    {
        var store = history.Store;

        if (!decoded.TryGetValue(block, out var page))
        {
            var count = store.GetWriteCount(block, position);

            page = count > 0 ? WorkfilePageReader.Read(store.GetImage(block, count - 1).Data, history.GetWorkfileLayout(block)) : null;

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

    private static string GetDirectoryName(TraceHashPartition partition, TraceHashPage page)
        => partition.DirectoryCount > 1
            ? $"{DirectoryName} Entry {(page.FirstBucket >> DirectoryShift) & (partition.DirectoryCount - 1)}"
            : DirectoryName;

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

    private static object GetBucketCell(LoadedTable loaded, int bucket, int current)
    {
        var text = GetText(bucket);

        if (bucket != current || loaded.Access is not { } access)
        {
            return text;
        }

        var badge = access.IsInsert ? InsertBadge : access.IsMatch ? MatchBadge : NoMatchBadge;

        return new BadgeText([], text, [badge]);
    }

    private void Load()
    {
        var history = History;

        var group = Group;

        var accesses = Accesses;

        var position = Position;

        var hexAddress = HexAddress;

        Loader.Request(() => Read(history, group, accesses, position, hexAddress));
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
        var table = loaded.Table;

        var root = new MemoryHashTableNode(MemoryHashTableNodeType.Group, GroupName, table.Group, -1, null);

        var heading = new MemoryHashTableNode(MemoryHashTableNodeType.Partitions, PartitionsName, 0, -1, root);

        root.Children.Add(heading);

        foreach (var partition in table.Partitions)
        {
            heading.Children.Add(CreatePartition(loaded, partition, heading));
        }

        var key = string.Join("|", Flatten(root).Select(n => $"{n.Key} {n.Detail} {string.Join(",", n.Badges)}"));

        if (key == StructureKey)
        {
            return;
        }

        StructureKey = key;

        var nodes = new Dictionary<ulong, MemoryHashTableNode> { [table.Group + TraceHashTableLayout.PartitionListOffset] = root };

        foreach (var node in Flatten(root).Where(n => n.IsSelectable))
        {
            nodes.TryAdd(node.Address, node);

            if (node.Type == MemoryHashTableNodeType.Partition)
            {
                nodes.TryAdd(node.Address + TraceHashTableLayout.PartitionLinkOffset, node);
            }
        }

        Nodes = nodes;

        Roots = [root];

        SelectedNode = Find(SelectedKey) ?? root;

        SelectedKey = SelectedNode.Key;
    }

    private MemoryHashTableNode CreatePartition(LoadedTable loaded, TraceHashPartition partition, MemoryHashTableNode parent)
    {
        var node = new MemoryHashTableNode(MemoryHashTableNodeType.Partition,
                                           $"Partition {partition.Index}",
                                           partition.Address,
                                           partition.Index,
                                           parent)
        {
            Badges = GetBadges(loaded.Table, partition)
        };

        foreach (var pages in partition.Pages.GroupBy(p => p.PointerPage))
        {
            node.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.Directory,
                                                      GetDirectoryName(partition, pages.First()),
                                                      partition.Directory,
                                                      partition.Index,
                                                      node)
            {
                Target = pages.Key
            });
        }

        List<PageEntry> overflow = partition.Index < loaded.OverflowPages.Count ? loaded.OverflowPages[partition.Index] : [];

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

        if (partition.Index < loaded.Workfiles.Count && loaded.Workfiles[partition.Index] is { Count: > 0 } workfiles)
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

        if (target.Key != SelectedKey)
        {
            SelectedKey = target.Key;

            SelectedItem = null;

            SelectedFieldRow = null;

            SelectedEntryRow = null;

            SelectedChangeSpan = null;
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

        var partition = Roots.SelectMany(Flatten)
                             .FirstOrDefault(n => n.Type == MemoryHashTableNodeType.Partition && n.Partition == part.Partition);

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

    private void OutlineMarker(int offset)
    {
        if (MarkerLookup.FindAt(Markers, offset) is not { } marker)
        {
            SelectedChangeSpan = null;

            return;
        }

        SelectedChangeSpan = new LogRecordAnnotation
        {
            Offset = marker.StartPosition,
            Length = marker.EndPosition - marker.StartPosition + 1,
            Description = marker.Name,
            OutlineColour = marker.BackColour
        };
    }

    private void SelectPendingBucket()
    {
        if (PendingBucket is not { } pending)
        {
            return;
        }

        PendingBucket = null;

        var (partition, bucket) = pending;

        var nodes = Roots.SelectMany(Flatten).Where(n => n.Partition == partition).ToList();

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
            case MemoryHashTableNodeType.Group:
                ShowFields(loaded, node, TraceHashTableLayout.GroupFields);
                break;

            case MemoryHashTableNodeType.Partition:
                ShowFields(loaded, node, TraceHashTableLayout.PartitionFields);
                break;

            case MemoryHashTableNodeType.BucketPage:
            case MemoryHashTableNodeType.OverflowPage:
                ShowEntries(loaded, node);
                break;

            case MemoryHashTableNodeType.WorkfilePage:
                ShowPage(loaded);
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

        if (node.Partition < 0 || node.Partition >= loaded.Table.Partitions.Count)
        {
            EntryTable = null;

            return;
        }

        var partition = loaded.Table.Partitions[node.Partition];

        var page = node.Address;

        var hexBase = loaded.HexBase;

        var content = string.Join(",", partition.Buckets.Select(GetBucketKey));

        SetMarkers($"{node.Key} {content}", () => GetPageMarkers(partition, page, hexBase));

        var current = loaded.Location is { } location && location.Partition == node.Partition ? location.Bucket : -1;

        var key = $"{node.Key} {current} {content}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = node.Type == MemoryHashTableNodeType.BucketPage
                ? CreateBucketPageTable(loaded, partition, node, current)
                : CreateOverflowPageTable(loaded, partition, page, current);
        }

        if (SelectedItem is { } selected && EntryTable.Rows.FirstOrDefault(r => r.Id == (long)selected) is { } row)
        {
            if (!ReferenceEquals(row, SelectedEntryRow))
            {
                SelectedEntryRow = row;
            }

            OutlineItem();
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

    private QueryResultSet CreateBucketPageTable(LoadedTable loaded, TraceHashPartition partition, MemoryHashTableNode node, int current)
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
                GetBucketCell(loaded, bucket, current),
                hasHead ? GetItemCell(head!.Items[0].Address, slot) : null,
                hasItem ? GetItemCell(GetNext(item.Bucket, item.Position), 0) : null,
                GetText(hasItem ? item.Bucket.Items[item.Position].Entries.Count : 0)
            ];

            rows.Add(new ResultRow<long>(cells) { Id = (long)slot });
        }

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private QueryResultSet CreateOverflowPageTable(LoadedTable loaded, TraceHashPartition partition, ulong page, int current)
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
                GetBucketCell(loaded, bucket.Index, current),
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
            || SelectedNode is not { Partition: >= 0 } node
            || node.Partition >= loaded.Table.Partitions.Count
            || !GetItemOwners(loaded.Table.Partitions[node.Partition]).TryGetValue((ulong)row.Id, out var owner))
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

    private void OutlineItem()
    {
        if (SelectedItem is not { } item || Loaded is not { } loaded || !IsOnPage(item, HexAddress))
        {
            return;
        }

        Outline(loaded.HexBase + (int)(item - HexAddress), TraceHashTableLayout.BucketSize, GetAddressText(item), FieldColours[0]);
    }

    private void Outline(int offset, int length, string description, Colour colour)
    {
        SelectedChangeSpan = new LogRecordAnnotation
        {
            Offset = offset,
            Length = length,
            Description = description,
            OutlineColour = colour.ToWindowsColor()
        };

        ScrollToOffset = offset;
    }

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

    private void ShowPage(LoadedTable loaded)
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
                WorkfilePage.Load(History, PageBlock);
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

    private sealed record LoadedTable(TraceHashTable Table,
                                      TraceHashAccess? Access,
                                      (int Partition, int Bucket)? Location,
                                      List<List<PageEntry>> Workfiles,
                                      List<List<PageEntry>> OverflowPages,
                                      Dictionary<ulong, List<(string Name, string Value)>> Rows,
                                      ulong HexAddress,
                                      int HexBlock,
                                      int HexBase,
                                      byte[]? Data,
                                      List<MemoryHistoryPointer> Pointers);
}

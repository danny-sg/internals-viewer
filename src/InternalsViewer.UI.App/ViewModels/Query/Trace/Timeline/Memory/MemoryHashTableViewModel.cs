using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Results;
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

    private const string BucketPageName = "Bucket Page";

    private const string WorkfilePagesName = "Workfile Pages";

    private const string WorkfilePageName = "Workfile Page";

    private const string ActiveBadge = "Active";

    private const string FirstBadge = "First";

    private const string LastBadge = "Last";

    private const string InsertBadge = "Insert";

    private const string MatchBadge = "Match";

    private const string NoMatchBadge = "No Match";

    private const string NotAllocated = "Not Allocated At This Position";

    private const string NotReadable = "No Hash Table At This Position";

    private const int HueWheel = 256;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const int FieldWidth = 200;

    private const int OffsetWidth = 80;

    private const int ValueWidth = 360;

    private const int BucketWidth = 130;

    private const int RowCountWidth = 90;

    private const int KeyWidth = 100;

    private const int RowWidth = 150;

    private const int ColumnWidth = 120;

    private const int PageSize = 8192;

    private const int FixedEntryColumns = 4;

    private const int DirectoryShift = 16;

    private const int ActiveField = 1;

    private const int LastField = 2;

    private const int FirstField = 3;

    private static readonly TimeSpan LoadInterval = TimeSpan.FromMilliseconds(50);

    private static readonly int ColourCount = new[]
    {
        TraceHashTableLayout.GroupFields.Count,
        TraceHashTableLayout.PartitionFields.Count,
        TraceHashTableLayout.BucketFields.Count
    }.Max();

    private static readonly List<Colour> FieldColours =
    [
        .. Enumerable.Range(0, ColourCount).Select(i => ColourHelpers.HsvToColor(i * HueWheel / ColourCount, FieldSaturation, FieldValue))
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

        Load();
    }

    public string Title => $"Hash Table 0x{Group:X16}";

    public MemoryPageViewModel WorkfilePage { get; }

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

    private int? SelectedBucket { get; set; }

    private (int Partition, int Bucket)? PendingBucket { get; set; }

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

        SelectedBucket = (int)id;

        OutlineBucket();
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

        var workfiles = new List<List<WorkfilePageEntry>>();

        foreach (var partition in table.Partitions)
        {
            var entries = new List<WorkfilePageEntry>();

            foreach (var bucket in partition.Buckets)
            {
                var block = bucket.Row == 0 ? -1 : store.GetBlock(bucket.Row, position);

                if (block < 0)
                {
                    continue;
                }

                var address = store.Blocks[block].Address;

                var found = entries.FindIndex(e => e.Address == address);

                if (found < 0)
                {
                    entries.Add(new WorkfilePageEntry(address, 1));
                }
                else
                {
                    entries[found] = entries[found] with { RowCount = entries[found].RowCount + 1 };
                }

                if (ReadRow(history, decoded, block, bucket.Row, position) is { } values)
                {
                    rows[bucket.Row] = values;
                }
            }

            entries.Sort((left, right) => left.Address.CompareTo(right.Address));

            workfiles.Add(entries);
        }

        var hexBlock = hexAddress == 0 ? -1 : store.GetBlock(hexAddress, position);

        var count = hexBlock >= 0 ? store.GetWriteCount(hexBlock, position) : 0;

        var data = count > 0 ? store.GetImage(hexBlock, count - 1).Data : null;

        var hexBase = hexBlock >= 0 ? (int)(hexAddress - store.Blocks[hexBlock].Address) : 0;

        return new LoadedTable(table,
                               access,
                               access is { } value ? table.GetLocation(value.Hash) : null,
                               workfiles,
                               rows,
                               hexAddress,
                               hexBlock,
                               hexBase,
                               data);
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
            var directory = new MemoryHashTableNode(MemoryHashTableNodeType.Directory,
                                                    GetDirectoryName(partition, pages.First()),
                                                    partition.Directory,
                                                    partition.Index,
                                                    node)
            {
                Target = pages.Key
            };

            foreach (var page in pages.Where(p => p.BucketPage != 0))
            {
                directory.Children.Add(new MemoryHashTableNode(MemoryHashTableNodeType.BucketPage,
                                                               BucketPageName,
                                                               page.BucketPage,
                                                               partition.Index,
                                                               directory)
                {
                    FirstBucket = page.FirstBucket,
                    Detail = GetBucketRange(partition, page)
                });
            }

            node.Children.Add(directory);
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
                    Detail = GetRowText(page.RowCount)
                });
            }

            node.Children.Add(heading);
        }

        return node;
    }

    private MemoryHashTableNode? Find(string key) => Roots.SelectMany(Flatten).FirstOrDefault(n => n.Key == key);

    private void Select(MemoryHashTableNode node, int? bucket = null)
    {
        var target = Find(node.Key) ?? node;

        if (target.Key != SelectedKey)
        {
            SelectedKey = target.Key;

            SelectedBucket = null;

            SelectedFieldRow = null;

            SelectedEntryRow = null;

            SelectedChangeSpan = null;
        }

        if (bucket is not null)
        {
            SelectedBucket = bucket;
        }

        IsSelecting = true;

        SelectedNode = target;

        IsSelecting = false;

        Show();
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

        Select(node, bucket);
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

        var buckets = partition.Buckets.Where(b => IsInRange(b.Index, node.FirstBucket)).ToList();

        var page = node.Address;

        var hexBase = loaded.HexBase;

        SetMarkers($"{node.Key} {string.Join(",", buckets.Select(b => b.Index))}",
                   () => buckets.SelectMany(b => GetBucketMarkers(b, hexBase + (int)(b.Address - page))));

        var current = loaded.Location is { } location
                      && location.Partition == node.Partition
                      && IsInRange(location.Bucket, node.FirstBucket)
            ? location.Bucket
            : -1;

        var fields = buckets.Select(b => loaded.Rows.GetValueOrDefault(b.Row)).FirstOrDefault(r => r is not null) ?? [];

        var key = $"{node.Key} {current} {string.Join(",", buckets.Select(b => $"{b.Index}:{b.Count}:{b.Key}:{b.Row:X}"))} {fields.Count}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = CreateEntryTable(loaded, buckets, fields, current);
        }

        if (SelectedBucket is { } selected && EntryTable.Rows.FirstOrDefault(r => r.Id == selected) is { } row)
        {
            if (!ReferenceEquals(row, SelectedEntryRow))
            {
                SelectedEntryRow = row;
            }

            OutlineBucket();
        }
    }

    private static IEnumerable<Marker> GetBucketMarkers(TraceHashBucket bucket, int offset)
        => TraceHashTableLayout.BucketFields.Select((f, i) => CreateMarker($"Bucket {bucket.Index} {f.Name}",
                                                                            offset + f.Offset,
                                                                            f.Length,
                                                                            i));

    private QueryResultSet CreateEntryTable(LoadedTable loaded,
                                            List<TraceHashBucket> buckets,
                                            List<(string Name, string Value)> fields,
                                            int current)
    {
        var layout = TraceHashTableLayout.BucketFields;

        var columns = new List<ResultColumn>
        {
            new(0, "Bucket", typeof(string), false) { Width = BucketWidth },
            new(1, layout[0].Name, typeof(string), false) { Width = RowCountWidth, MarkerColour = FieldColours[0] },
            new(2, layout[1].Name, typeof(string), false) { Width = KeyWidth, MarkerColour = FieldColours[1] },
            new(3, layout[2].Name, typeof(string), false) { Width = RowWidth, MarkerColour = FieldColours[2] }
        };

        columns.AddRange(fields.Select((f, i) => new ResultColumn(FixedEntryColumns + i, f.Name, typeof(string), true)
        {
            Width = ColumnWidth
        }));

        var rows = new List<ResultRow<long>>();

        if (current >= 0 && buckets.All(b => b.Index != current))
        {
            var empty = new object?[columns.Count];

            empty[0] = GetBucketCell(loaded, current, current);

            empty[1] = GetText(0);

            rows.Add(new ResultRow<long>(empty) { Id = current });
        }

        foreach (var bucket in buckets)
        {
            var values = loaded.Rows.GetValueOrDefault(bucket.Row) ?? [];

            var row = bucket.Row;

            object?[] cells =
            [
                GetBucketCell(loaded, bucket.Index, current),
                GetText(bucket.Count),
                GetText(bucket.Key),
                row == 0 ? null : new ActionLinks([new ActionLink($"0x{row:X16}", () => OpenRow(row))]),
                .. fields.Select(f => (object?)values.FirstOrDefault(v => v.Name == f.Name).Value)
            ];

            rows.Add(new ResultRow<long>(cells) { Id = bucket.Index });
        }

        rows.Sort((left, right) => left.Id.CompareTo(right.Id));

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private void OutlineBucket()
    {
        if (SelectedBucket is not { } index
            || Loaded is not { } loaded
            || SelectedNode is not { Partition: >= 0 } node
            || node.Partition >= loaded.Table.Partitions.Count
            || loaded.Table.Partitions[node.Partition].Buckets.FirstOrDefault(b => b.Index == index) is not { Address: > 0 } bucket
            || bucket.Address < HexAddress
            || bucket.Address >= HexAddress + PageSize)
        {
            return;
        }

        Outline(loaded.HexBase + (int)(bucket.Address - HexAddress), TraceHashTableLayout.BucketSize, $"Bucket {index}", FieldColours[0]);
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

        if (page is null)
        {
            return;
        }

        PendingSlotOffset = (int)(row - page.Address);

        Select(page);
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

    private readonly record struct WorkfilePageEntry(ulong Address, int RowCount);

    private sealed record LoadedTable(TraceHashTable Table,
                                      TraceHashAccess? Access,
                                      (int Partition, int Bucket)? Location,
                                      List<List<WorkfilePageEntry>> Workfiles,
                                      Dictionary<ulong, List<(string Name, string Value)>> Rows,
                                      ulong HexAddress,
                                      int HexBlock,
                                      int HexBase,
                                      byte[]? Data);
}

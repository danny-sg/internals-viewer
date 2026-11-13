using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
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
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed class MemoryHashTableViewModel : MemoryStructureViewModel
{
    private const string GroupName = "Partition Group";

    private const string HashTableName = "Hash Table";

    private const string MemoryName = "Memory";

    private const string AccessorsName = "Accessors";

    private const string BucketPageName = "Item Page";

    private const string BucketPagesName = "Hash Bucket Pages";

    private const string OverflowPageName = "Overflow Page";

    private const string WorkfilePagesName = "Workfile Pages";

    private const string WorkfilePageName = "Workfile Page";

    private const string BitmapName = "Bitmap Pages";

    private const string BitmapPageName = "Bitmap Page";

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

    private const string NotReadable = "No Hash Table At This Position";

    private const string NoRows = "No Rows In This Bucket";

    private const int TypeWidth = 140;

    private const int AccessorWidth = 260;

    private const int BucketWidth = 130;

    private const int RowCountWidth = 90;

    private const int KeyWidth = 100;

    private const int RowWidth = 240;

    private const int ColumnWidth = 120;

    private const int EntryWidth = 70;

    private const int FixedEntryColumns = 3;

    private const int CountField = 0;

    private const int HeadField = 1;

    private const int NextField = 2;

    private const int KeyField = 3;

    private const int RowField = 4;

    private static readonly int ActiveField = GetField(TraceHashTableLayout.GroupFields, TraceHashTableLayout.ActivePartitionOffset);

    private static readonly int LastField = GetField(TraceHashTableLayout.GroupFields, TraceHashTableLayout.PartitionListOffset);

    private static readonly int FirstField = GetField(TraceHashTableLayout.GroupFields,
                                                      TraceHashTableLayout.PartitionListOffset + sizeof(ulong));


    public MemoryHashTableViewModel(MemoryHistoryViewModel history, ulong group, TracePosition position, ITraceNavigator navigator)
        : base(history, position, navigator)
    {
        Group = group;

        SelectedKey = new MemoryStructureNode(MemoryStructureNodeType.Group, GroupName, group, -1, null).Key;

        Loader = new PlayheadLoader<LoadedTable?>(LoadInterval, Apply);

        var calls = history.CallStore;

        var opened = calls is null ? [] : TraceHashTableReader.GetAccesses(calls, group);

        var at = opened.Count > 0 ? opened[0].Position : position;

        Iterator = TraceHashTableReader.GetIterator(history.Store, group, at) ?? 0;

        Groups = Iterator == 0
            ? []
            : TraceHashTableReader.GetIteratorGroups(history.Store, history.AddressMap, Iterator, at);

        if (!Groups.Exists(g => g.Group == group))
        {
            Groups.Insert(0, (0, group, false));
        }

        Accesses = calls is null
            ? []
            : TraceHashTableReader.GetAccesses(calls, Groups.Where(g => !g.IsWorktable).Select(g => g.Group).ToHashSet());

        Load();
    }

    public override string Title => $"Hash Table 0x{(Iterator == 0 ? Group : Iterator):X16}";

    public override string Key => $"HashTable:{(Iterator == 0 ? Group : Iterator):X}";

    protected override HexImage? Image => Loaded?.Image;

    private ulong Group { get; }

    private EngineLayout.HashIteratorLayout HashIterator => History.Store.Layout.HashIterator;

    private int CurrentField => GetField(HashIterator.TableFields, HashIterator.CurrentGroupField.Offset);

    private List<Colour> FieldColours => field ??= CreateColours(new[]
    {
        HashIterator.TableFields.Count,
        TraceHashTableLayout.GroupFields.Count,
        TraceHashTableLayout.PartitionFields.Count,
        TraceHashTableLayout.ItemFields.Count
    }.Max());

    private ulong Iterator { get; }

    private List<(int Slot, ulong Group, bool IsWorktable)> Groups { get; }

    private PlayheadLoader<LoadedTable?> Loader { get; }

    private Dictionary<ulong, List<TraceHashAccess>> Accesses { get; }

    private LoadedTable? Loaded { get; set; }

    private Task<List<TraceMemoryUsage>> MemoryUsage => field ??= ReadMemoryUsage(History, Iterator);

    private (ulong Group, int Partition, int Bucket)? PendingBucket { get; set; }

    private (ulong Group, TraceHashPart Part)? PendingPart { get; set; }

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

    public override void SelectOffset(int offset)
    {
        if (SelectedNode is not { } node || Loaded is not { } loaded)
        {
            return;
        }

        var previous = MaskedMarker;

        if (node.Type is MemoryStructureNodeType.HashTable
                      or MemoryStructureNodeType.Group
                      or MemoryStructureNodeType.WorktableGroup
                      or MemoryStructureNodeType.Accessors
                      or MemoryStructureNodeType.Partition
                      or MemoryStructureNodeType.Bitmap)
        {
            SelectFieldAt(offset);
        }
        else if (node.Type is MemoryStructureNodeType.BucketPage or MemoryStructureNodeType.OverflowPage
                 && GetPartition(loaded, node) is not null)
        {
            var size = (ulong)TraceHashTableLayout.BucketSize;

            var item = HexAddress + (ulong)(offset - loaded.Image.Offset) / size * size;

            if (EntryTable?.Rows.FirstOrDefault(r => r.Id == (long)item) is { } row)
            {
                SelectedEntryRow = row;
            }

            return;
        }
        else if (node.Type == MemoryStructureNodeType.WorkfilePage)
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

    private static Task<List<TraceMemoryUsage>> ReadMemoryUsage(MemoryHistoryViewModel history, ulong iterator)
        => iterator != 0 && history.CallStore is { } calls
            ? Task.Run(() => TraceHashTableReader.GetMemory(history.Store, history.AddressMap, calls, iterator))
            : Task.FromResult<List<TraceMemoryUsage>>([]);

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

        return new LoadedTable(iterator,
                               iterator == 0 ? 0 : TraceHashTableReader.GetCurrentGroup(store, iterator, position),
                               loaded,
                               rows,
                               HexImage.Read(history, hexAddress, position));
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

    private List<MemoryStructureBadge> GetBadges(TraceHashTable table, TraceHashPartition partition)
    {
        var badges = new List<MemoryStructureBadge>();

        if (partition.Address == table.ActivePartition)
        {
            badges.Add(new MemoryStructureBadge(ActiveBadge, FieldColours[ActiveField].ToWindowsColor()));
        }

        if (partition.Index == 0)
        {
            badges.Add(new MemoryStructureBadge(FirstBadge, FieldColours[FirstField].ToWindowsColor()));
        }

        if (partition.Index == table.Partitions.Count - 1)
        {
            badges.Add(new MemoryStructureBadge(LastBadge, FieldColours[LastField].ToWindowsColor()));
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

    private static string GetPageText(int count) => count == 1 ? "1 Page" : $"{count} Pages";

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

    private static string GetText(long value) => value.ToString(CultureInfo.CurrentCulture);

    private static string GetGroupName(LoadedGroup group) => HashTablePartLabel.GetGroupName(group.Slot, group.IsWorktable);

    private static LoadedGroup? GetLoadedGroup(LoadedTable loaded, MemoryStructureNode node)
        => loaded.Groups.Find(g => g.Address == node.Group);

    private static TraceHashPartition? GetPartition(LoadedTable loaded, MemoryStructureNode node)
        => node.Partition >= 0 && GetLoadedGroup(loaded, node)?.Table is { } table && node.Partition < table.Partitions.Count
            ? table.Partitions[node.Partition]
            : null;

    private static bool IsInRange(int bucket, int first) => bucket >= first && bucket < first + TraceHashTableLayout.BucketsPerPage;

    private Marker CreateMarker(string name, int offset, int length, int colour)
        => CreateMarker(name, offset, length, FieldColours[colour]);

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

    protected override void Load()
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

        if (PendingPart is null && PendingBucket is null)
        {
            RememberFirst();
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
        var root = new MemoryStructureNode(MemoryStructureNodeType.HashTable, HashTableName, loaded.Iterator, -1, null);

        root.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.Memory, MemoryName, 0, -1, root));

        foreach (var side in loaded.Groups.GroupBy(g => g.Slot / TraceHashTableLayout.GroupsPerSide).OrderBy(s => s.Key))
        {
            var name = HashTablePartLabel.GetSideName(side.First().Slot);

            var heading = side.Key == 0
                ? new MemoryStructureNode(MemoryStructureNodeType.BuildSide, name, 0, -1, root)
                : new MemoryStructureNode(MemoryStructureNodeType.ProbeSide, name, 0, -1, root);

            foreach (var group in side.OrderBy(g => g.Slot))
            {
                var type = group.IsWorktable ? MemoryStructureNodeType.WorktableGroup : MemoryStructureNodeType.Group;

                var node = new MemoryStructureNode(type,
                                                   GetGroupName(group),
                                                   group.Address,
                                                   -1,
                                                   heading)
                {
                    IsActive = Accesses.TryGetValue(group.Address, out var used) && used.Count > 0,
                    Badges = group.Address == loaded.CurrentGroup
                        ? [new MemoryStructureBadge(InUseBadge, FieldColours[CurrentField].ToWindowsColor())]
                        : []
                };

                if (group.Table is { Columns.Count: > 0 } table)
                {
                    node.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.Accessors, AccessorsName, table.ColumnArray, -1, node)
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

        var nodes = new Dictionary<ulong, MemoryStructureNode>();

        foreach (var node in Flatten(root).Where(n => n.IsSelectable))
        {
            nodes.TryAdd(node.Address, node);

            if (node.Type == MemoryStructureNodeType.Group)
            {
                nodes.TryAdd(node.Address + TraceHashTableLayout.PartitionListOffset, node);
            }

            if (node.Type == MemoryStructureNodeType.Partition)
            {
                nodes.TryAdd(node.Address + TraceHashTableLayout.PartitionLinkOffset, node);
            }
        }

        Nodes = nodes;

        Roots = [root];

        SelectedNode = Find(SelectedKey) ?? Flatten(root).FirstOrDefault(n => n.Type == MemoryStructureNodeType.Group) ?? root;

        SelectedKey = SelectedNode.Key;
    }

    private MemoryStructureNode CreatePartition(LoadedGroup group, TraceHashPartition partition, MemoryStructureNode parent)
    {
        var node = new MemoryStructureNode(MemoryStructureNodeType.Partition,
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
            var heading = new MemoryStructureNode(MemoryStructureNodeType.BucketPages, BucketPagesName, 0, partition.Index, node);

            foreach (var page in partition.Pages.Where(p => p.BucketPage != 0).OrderBy(p => p.FirstBucket))
            {
                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.BucketPage,
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
                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.OverflowPage,
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
            var heading = new MemoryStructureNode(MemoryStructureNodeType.WorkfilePages, WorkfilePagesName, 0, partition.Index, node);

            foreach (var page in workfiles)
            {
                heading.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.WorkfilePage,
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

        if (partition.Bitmap != 0)
        {
            var bitmap = new MemoryStructureNode(MemoryStructureNodeType.Bitmap, BitmapName, partition.Bitmap, partition.Index, node)
            {
                Detail = GetPageText(partition.BitmapPages.Count)
            };

            for (var index = 0; index < partition.BitmapPages.Count; index++)
            {
                bitmap.Children.Add(new MemoryStructureNode(MemoryStructureNodeType.BitmapPage,
                                                            $"{BitmapPageName} {index + 1}",
                                                            partition.BitmapPages[index],
                                                            partition.Index,
                                                            bitmap));
            }

            node.Children.Add(bitmap);
        }

        return node;
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
                             .FirstOrDefault(n => n.Type == MemoryStructureNodeType.Partition
                                                  && n.Group == group
                                                  && n.Partition == part.Partition);

        if (partition is null)
        {
            Show();

            return;
        }

        Select(partition);
    }

    protected override MemoryStructureNode? GetPointerNode(ulong value)
        => Nodes.TryGetValue(value, out var node)
            ? node
            : Roots.SelectMany(Flatten)
                   .FirstOrDefault(n => n.Type is MemoryStructureNodeType.BucketPage
                                               or MemoryStructureNodeType.OverflowPage
                                               or MemoryStructureNodeType.WorkfilePage
                                        && IsOnPage(value, n.Address));

    protected override void OpenPointer(MemoryStructureNode node, ulong value)
    {
        if (node.Type == MemoryStructureNodeType.WorkfilePage)
        {
            OpenRow(value);

            return;
        }

        Select(node);
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

        var node = nodes.FirstOrDefault(n => n.Type == MemoryStructureNodeType.BucketPage && IsInRange(bucket, n.FirstBucket))
                   ?? nodes.FirstOrDefault(n => n.Type == MemoryStructureNodeType.Partition);

        if (node is null)
        {
            Show();

            return;
        }

        if (node.Type != MemoryStructureNodeType.BucketPage)
        {
            Select(node);

            return;
        }

        Select(node, node.Address + (ulong)((bucket - node.FirstBucket) * TraceHashTableLayout.BucketSize));
    }

    protected override void ShowDetail(MemoryStructureNode node)
    {
        if (Loaded is not { } loaded)
        {
            return;
        }

        switch (node.Type)
        {
            case MemoryStructureNodeType.HashTable:
                ShowFields(node, HashIterator.TableFields, FieldColours);
                break;

            case MemoryStructureNodeType.Group:
                ShowFields(node, TraceHashTableLayout.GroupFields, FieldColours);
                break;

            case MemoryStructureNodeType.WorktableGroup:
                ShowFields(node, [], FieldColours);
                break;

            case MemoryStructureNodeType.Accessors:
                ShowAccessors(loaded, node);
                break;

            case MemoryStructureNodeType.Partition:
                ShowFields(node, TraceHashTableLayout.PartitionFields, FieldColours);
                break;

            case MemoryStructureNodeType.BucketPage:
            case MemoryStructureNodeType.OverflowPage:
                ShowEntries(loaded, node);
                break;

            case MemoryStructureNodeType.WorkfilePage:
                ShowPage(GetLoadedGroup(loaded, node)?.Layout);
                break;

            case MemoryStructureNodeType.Bitmap:
                ShowBitmap(loaded, node);
                break;

            case MemoryStructureNodeType.BitmapPage:
                ShowFields(node, [], FieldColours);
                break;

            case MemoryStructureNodeType.Memory:
                ShowMemory(MemoryUsage);
                break;

            default:
                ClearDetail();
                break;
        }
    }

    private void ShowAccessors(LoadedTable loaded, MemoryStructureNode node)
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

        ShownBase = loaded.Image.Offset;

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

    private void ShowBitmap(LoadedTable loaded, MemoryStructureNode node)
    {
        var pages = GetPartition(loaded, node)?.BitmapPages ?? [];

        var data = Image?.Data;

        var offset = Image?.Offset ?? 0;

        var fields = new List<MemoryField>();

        for (var slot = 0; data is not null && offset + slot + sizeof(ulong) <= data.Length; slot += sizeof(ulong))
        {
            if (ReadValue(data, offset + slot, sizeof(ulong)) is { } value && pages.Contains(value))
            {
                fields.Add(new MemoryField($"{BitmapPageName} {fields.Count + 1}", slot, sizeof(ulong)));
            }
        }

        ShowFields(node, fields, [.. fields.Select((_, i) => FieldColours[i % FieldColours.Count])]);
    }

    private static string GetColumnName(List<ColumnStructure>? names, TraceHashColumn column, int index)
        => names is not null && index < names.Count ? names[index].ColumnName : $"Column {column.Binding.Ordinal}";

    private void ShowEntries(LoadedTable loaded, MemoryStructureNode node)
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

        var hexBase = loaded.Image.Offset;

        var content = string.Join(",", partition.Buckets.Select(GetBucketKey));

        var current = group.Location is { } location && location.Partition == node.Partition ? location.Bucket : -1;

        var key = $"{node.Key} {current} {content}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            EntryTable = node.Type == MemoryStructureNodeType.BucketPage
                ? CreateBucketPageTable(group.Access, partition, node, current)
                : CreateOverflowPageTable(group.Access, partition, page, current);
        }

        SelectItemRow();

        ShowItemMarkers($"{node.Key} {content}", () => GetPageMarkers(partition, page, hexBase), TraceHashTableLayout.BucketSize);
    }

    private IEnumerable<Marker> GetPageMarkers(TraceHashPartition partition, ulong page, int hexBase)
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
                                                 MemoryStructureNode node,
                                                 int current)
    {
        var fields = TraceHashTableLayout.ItemFields;

        var columns = new List<ResultColumn>
        {
            new(0, "Bucket", typeof(string), false) { Width = BucketWidth },
            new(1, fields[HeadField].Name, typeof(string), false) { Width = AddressWidth, MarkerColour = FieldColours[HeadField] },
            new(2, fields[NextField].Name, typeof(string), false) { Width = AddressWidth, MarkerColour = FieldColours[NextField] },
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
            new(1, fields[NextField].Name, typeof(string), false) { Width = AddressWidth, MarkerColour = FieldColours[NextField] },
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

    protected override ResultDetails GetEntryDetails(ResultRow<long> row)
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

    private void OpenRow(ulong row)
    {
        var page = Roots.SelectMany(Flatten)
                        .FirstOrDefault(n => n.Type == MemoryStructureNodeType.WorkfilePage
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

    private readonly record struct PageEntry(ulong Address, int Count);

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
                                      HexImage Image);
}

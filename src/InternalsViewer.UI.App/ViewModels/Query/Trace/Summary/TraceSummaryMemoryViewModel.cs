using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History.Graph;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public sealed partial class TraceSummaryMemoryViewModel : TraceSummaryPaneViewModel
{
    private const string MemoryTitle = "Memory";

    private const int NodeLimit = 50000;

    private const int ExpandAllLimit = 500;

    private const string NotAvailable = "No Memory History In This Run";

    private const string NoMatch = "No Objects Match";

    private const string HashTablePrefix = "Hash Table › ";

    private static readonly Dictionary<string, string> KnownNames = new(StringComparer.Ordinal)
    {
        [TraceHashTableLayout.GroupClass] = HashTablePrefix + "Partition Group",
        [TraceHashTableLayout.PartitionClass] = HashTablePrefix + "Partition",
        ["CHashWorktablePartitionGroup"] = HashTablePrefix + "Worktable Partition Group"
    };

    [ObservableProperty]
    private bool _isStackVisible;

    [ObservableProperty]
    private string _filter = string.Empty;

    public TraceSummaryMemoryViewModel(MemoryHistoryViewModel history, Func<ITraceNavigator> getNavigator)
        : base(MemoryTitle, history, getNavigator)
    {
        Loader = new PlayheadLoader<LoadedTree?>(TimeSpan.Zero, Apply, b => IsLoading = b);
    }

    public override bool IsFiltered => Kept is not null;

    public override bool IsExpandingAll => IsFiltered && KeptCount <= ExpandAllLimit;

    private int KeptCount { get; set; }

    private PlayheadLoader<LoadedTree?> Loader { get; }

    private LoadedTree? Loaded { get; set; }

    private bool[]? Kept { get; set; }

    public override List<TraceSummaryNode> GetChildren(TraceSummaryNode node)
    {
        if (node.Children is { } children)
        {
            return children;
        }

        if (Loaded is not { } loaded)
        {
            return [];
        }

        if (node.Members is { } members)
        {
            return [.. members.Select(m => CreateInstance(loaded, m, isGrouped: true))];
        }

        return node.Entry >= 0 ? CreateNodes(loaded, loaded.Children[node.Entry], node.Key) : [];
    }

    protected override void Build()
    {
        if (!History.IsAvailable || History.Timeline is not { } timeline || History.CallStore is not { } calls)
        {
            EmptyText = NotAvailable;

            return;
        }

        EmptyText = string.Empty;

        var history = History;

        var from = History.WindowStart;

        var to = History.WindowEnd;

        Loader.Request(() => Read(history, timeline, calls, from, to));
    }

    protected override void Clear()
    {
        Loader.Cancel();

        Loaded = null;

        Kept = null;
    }

    partial void OnIsStackVisibleChanged(bool value) => ShowTree();

    partial void OnFilterChanged(string value) => ShowTree();

    private static LoadedTree Read(MemoryHistoryViewModel history,
                                   TraceTimeline timeline,
                                   TraceCallStore calls,
                                   TracePosition from,
                                   TracePosition to)
    {
        var store = history.Store;

        var paths = new MemoryHistoryCallPathResolver(store, timeline, calls);

        var tree = MemoryHistoryObjectTree.Build(store, from, to, NodeLimit, paths.GetPath);

        var hashTables = GetHashTableBlocks(history, calls);

        var positions = GetFirstPositions(history, tree);

        var entries = new List<TreeEntry>(tree.Nodes.Count);

        var children = new List<List<int>>(tree.Nodes.Count);

        var roots = new List<int>();

        for (var index = 0; index < tree.Nodes.Count; index++)
        {
            var node = tree.Nodes[index];

            var parentKey = node.Parent >= 0 ? entries[node.Parent].Key : string.Empty;

            entries.Add(node.IsBlock
                            ? CreateBlockEntry(history, hashTables, node.Block)
                            : CreateCallEntry(history, node, positions[index] ?? from, parentKey));

            children.Add([]);

            if (node.Parent < 0)
            {
                roots.Add(index);
            }
            else
            {
                children[node.Parent].Add(index);
            }
        }

        return new LoadedTree(entries, children, roots);
    }

    private static Dictionary<int, (ulong Group, TraceHashPart Part)> GetHashTableBlocks(MemoryHistoryViewModel history,
                                                                                         TraceCallStore calls)
    {
        var store = history.Store;

        var blocks = new Dictionary<int, (ulong Group, TraceHashPart Part)>();

        foreach (var group in TraceHashTableReader.GetGroups(calls))
        {
            var accesses = TraceHashTableReader.GetAccesses(calls, group);

            if (accesses.Count == 0 || TraceHashTableReader.Read(store, group, accesses[^1].Position) is not { } table)
            {
                continue;
            }

            foreach (var part in table.GetParts())
            {
                if (part.Address != 0 && store.GetBlock(part.Address, accesses[^1].Position) is >= 0 and var block)
                {
                    blocks.TryAdd(block, (group, part));
                }
            }
        }

        return blocks;
    }

    private static string GetName(MemoryHistoryViewModel history, int block, string type, TraceHashPart? part)
    {
        if (part is { } found)
        {
            return HashTablePrefix + HashTablePartLabel.GetName(found.Type);
        }

        return history.GetOperatorName(block) ?? KnownNames.GetValueOrDefault(type) ?? type;
    }

    private static TreeEntry CreateBlockEntry(MemoryHistoryViewModel history,
                                              Dictionary<int, (ulong Group, TraceHashPart Part)> hashTables,
                                              int block)
    {
        var info = history.Store.Blocks[block];

        var type = history.GetTypeName(block);

        TraceHashPart? part = hashTables.TryGetValue(block, out var hash) ? hash.Part : null;

        return new TreeEntry(block,
                             info.Address,
                             info.IsStack,
                             GetName(history, block, type, part),
                             history.GetObjectName(block),
                             type,
                             hash.Group,
                             part,
                             $"#{block}",
                             null);
    }

    private static TreeEntry CreateCallEntry(MemoryHistoryViewModel history,
                                             MemoryHistoryObjectTreeNode node,
                                             TracePosition position,
                                             string parentKey)
    {
        var block = node.Instance != 0 ? history.Store.GetBlock(node.Instance, position) : -1;

        var key = node.Instance != 0 ? $"{parentKey}/@{node.Instance:X}" : $"{parentKey}/{node.ClassName}";

        if (block >= 0 && history.Store.Blocks[block].Address == node.Instance)
        {
            var type = history.GetTypeName(block);

            return new TreeEntry(block,
                                 node.Instance,
                                 false,
                                 GetName(history, block, type, null),
                                 history.GetObjectName(block),
                                 type,
                                 0,
                                 null,
                                 key,
                                 position);
        }

        return new TreeEntry(block,
                             node.Instance,
                             false,
                             KnownNames.GetValueOrDefault(node.ClassName) ?? node.ClassName,
                             node.ClassName,
                             node.ClassName,
                             0,
                             null,
                             key,
                             position);
    }

    private static TracePosition?[] GetFirstPositions(MemoryHistoryViewModel history, MemoryHistoryObjectTree tree)
    {
        var positions = new TracePosition?[tree.Nodes.Count];

        for (var index = 0; index < tree.Nodes.Count; index++)
        {
            var node = tree.Nodes[index];

            if (!node.IsBlock)
            {
                continue;
            }

            var position = history.Store.Blocks[node.Block].FromPosition;

            for (var parent = node.Parent; parent >= 0 && positions[parent] is null; parent = tree.Nodes[parent].Parent)
            {
                positions[parent] = position;
            }
        }

        return positions;
    }

    private static bool[]? GetKept(LoadedTree loaded, string filter)
    {
        if (filter.Length == 0)
        {
            return null;
        }

        var address = filter.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? filter[2..] : filter;

        var kept = new bool[loaded.Entries.Count];

        for (var index = 0; index < kept.Length; index++)
        {
            var entry = loaded.Entries[index];

            kept[index] = entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                          || entry.ObjectName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                          || entry.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                          || (address.Length > 0 && GetAddressText(entry.Address).Contains(address, StringComparison.OrdinalIgnoreCase));
        }

        var parents = new int[loaded.Entries.Count];

        Array.Fill(parents, -1);

        for (var index = 0; index < loaded.Children.Count; index++)
        {
            foreach (var child in loaded.Children[index])
            {
                parents[child] = index;
            }
        }

        for (var index = kept.Length - 1; index >= 0; index--)
        {
            if (kept[index] && parents[index] >= 0)
            {
                kept[parents[index]] = true;
            }
        }

        return kept;
    }

    private void Apply(LoadedTree? loaded)
    {
        Loaded = loaded;

        if (loaded is null)
        {
            EmptyText = NotAvailable;

            return;
        }

        ShowTree();
    }

    private void ShowTree()
    {
        if (Loaded is not { } loaded)
        {
            return;
        }

        Kept = GetKept(loaded, Filter.Trim());

        KeptCount = Kept?.Count(k => k) ?? loaded.Entries.Count;

        Roots = CreateNodes(loaded, loaded.Roots, string.Empty);

        EmptyText = Roots.Count == 0 ? IsFiltered ? NoMatch : NotAvailable : string.Empty;
    }

    private List<int> GetVisible(LoadedTree loaded, List<int> indexes)
    {
        var visible = new List<int>();

        var pending = new Stack<int>(Enumerable.Reverse(indexes));

        while (pending.TryPop(out var index))
        {
            if (Kept is { } kept && !kept[index])
            {
                continue;
            }

            if (!IsStackVisible && loaded.Entries[index].IsStack)
            {
                var children = loaded.Children[index];

                for (var child = children.Count - 1; child >= 0; child--)
                {
                    pending.Push(children[child]);
                }

                continue;
            }

            visible.Add(index);
        }

        return visible;
    }

    private List<TraceSummaryNode> CreateNodes(LoadedTree loaded, List<int> indexes, string parentKey)
    {
        var nodes = new List<TraceSummaryNode>();

        var groups = GetVisible(loaded, indexes).GroupBy(i => loaded.Entries[i].Name)
                                                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            List<int> members = [.. group];

            if (members.Count == 1)
            {
                nodes.Add(CreateInstance(loaded, members[0], isGrouped: false));

                continue;
            }

            var count = members.Count.ToString("N0", CultureInfo.CurrentCulture);

            nodes.Add(new TraceSummaryNode(group.Key, $"{count} Objects", $"{parentKey}/{group.Key}")
            {
                Members = members,
                HasChildren = true
            });
        }

        return nodes;
    }

    private TraceSummaryNode CreateInstance(LoadedTree loaded, int index, bool isGrouped)
    {
        var entry = loaded.Entries[index];

        var address = entry.Address != 0 ? GetAddressText(entry.Address) : string.Empty;

        var name = entry.Name;

        if (isGrouped && (entry.ObjectName != entry.TypeName || address.Length > 0))
        {
            name = entry.ObjectName != entry.TypeName ? entry.ObjectName : address;
        }

        var detail = name == address ? string.Empty : address;

        return new TraceSummaryNode(name, detail, entry.Key)
        {
            Entry = index,
            Block = entry.Block,
            Address = entry.Address,
            Position = entry.Position,
            HashTableGroup = entry.HashTableGroup,
            HashTablePart = entry.HashTablePart,
            HasChildren = GetVisible(loaded, loaded.Children[index]).Count > 0
        };
    }

    private readonly record struct TreeEntry(int Block,
                                             ulong Address,
                                             bool IsStack,
                                             string Name,
                                             string ObjectName,
                                             string TypeName,
                                             ulong HashTableGroup,
                                             TraceHashPart? HashTablePart,
                                             string Key,
                                             TracePosition? Position);

    private sealed record LoadedTree(List<TreeEntry> Entries, List<List<int>> Children, List<int> Roots);
}

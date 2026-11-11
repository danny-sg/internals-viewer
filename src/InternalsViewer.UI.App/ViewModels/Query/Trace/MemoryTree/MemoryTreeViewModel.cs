using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History.Graph;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.UI.App.Models.Query.Trace.MemoryTree;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryTree;

public sealed partial class MemoryTreeViewModel : ObservableObject
{
    private const int NodeLimit = 50000;

    private const string IteratorPrefix = "Iterator › ";

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
    private List<MemoryTreeNode> _roots = [];

    [ObservableProperty]
    private bool _isStackVisible;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public MemoryTreeViewModel(MemoryHistoryViewModel history, Func<ITraceNavigator> getNavigator)
    {
        History = history;

        GetNavigator = getNavigator;

        Loader = new PlayheadLoader<LoadedTree?>(TimeSpan.Zero, Apply, b => IsLoading = b);
    }

    public bool IsFiltered => Kept is not null;

    public int KeptCount { get; private set; }

    private MemoryHistoryViewModel History { get; }

    private Func<ITraceNavigator> GetNavigator { get; }

    private PlayheadLoader<LoadedTree?> Loader { get; }

    private LoadedTree? Loaded { get; set; }

    private bool[]? Kept { get; set; }

    private bool IsShown { get; set; }

    private bool IsBuilt { get; set; }

    public void Show()
    {
        IsShown = true;

        if (!IsBuilt)
        {
            Build();
        }
    }

    public void Reset()
    {
        Loader.Cancel();

        Loaded = null;

        Kept = null;

        IsBuilt = false;

        Roots = [];

        Status = string.Empty;

        EmptyText = string.Empty;

        if (IsShown)
        {
            Build();
        }
    }

    public List<MemoryTreeNode> GetChildren(MemoryTreeNode node)
    {
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

    public void Open(MemoryTreeNode node)
    {
        if (node.IsObject && node.Block < History.Store.Blocks.Count)
        {
            GetNavigator().OpenMemory(node.Block, node.Address, History.Store.Blocks[node.Block].FromPosition);
        }
    }

    public void OpenHashTable(MemoryTreeNode node)
    {
        if (node.HashTableGroup != 0)
        {
            GetNavigator().OpenHashTable(node.HashTableGroup, node.HashTablePart);
        }
    }

    public void OpenGraph(MemoryTreeNode node)
    {
        if (node.IsObject && node.Block < History.Store.Blocks.Count)
        {
            GetNavigator().OpenReferenceGraph(node.Block, History.Store.Blocks[node.Block].FromPosition);
        }
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

        var creators = new MemoryHistoryCreatorResolver(store, timeline, calls);

        var tree = MemoryHistoryObjectTree.Build(store, from, to, NodeLimit, creators.GetCreator);

        var hashTables = GetHashTableBlocks(history, calls);

        var entries = new List<TreeEntry>(tree.Nodes.Count);

        var children = new List<List<int>>(tree.Nodes.Count);

        var roots = new List<int>();

        for (var index = 0; index < tree.Nodes.Count; index++)
        {
            var node = tree.Nodes[index];

            var block = store.Blocks[node.Block];

            var type = history.GetTypeName(node.Block);

            TraceHashPart? part = hashTables.TryGetValue(node.Block, out var hash) ? hash.Part : null;

            entries.Add(new TreeEntry(node.Block,
                                      block.Address,
                                      block.IsStack,
                                      GetName(history, node.Block, type, part),
                                      history.GetObjectName(node.Block),
                                      type,
                                      hash.Group,
                                      part));

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

        return new LoadedTree(entries, children, roots, tree.IsTruncated);
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

        return history.GetOperatorLabel(block) is { } label ? IteratorPrefix + label : KnownNames.GetValueOrDefault(type) ?? type;
    }

    private static string GetAddressText(ulong address) => $"0x{address:X16}";

    private void Build()
    {
        IsBuilt = true;

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

    private void Apply(LoadedTree? loaded)
    {
        Loaded = loaded;

        if (loaded is null)
        {
            EmptyText = NotAvailable;

            return;
        }

        var count = loaded.Entries.Count.ToString("N0", CultureInfo.CurrentCulture);

        Status = loaded.IsTruncated ? $"First {count} Objects" : $"{count} Objects";

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

    private List<MemoryTreeNode> CreateNodes(LoadedTree loaded, List<int> indexes, string parentKey)
    {
        var nodes = new List<MemoryTreeNode>();

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

            nodes.Add(new MemoryTreeNode(group.Key, $"{count} Objects", $"{parentKey}/{group.Key}")
            {
                Members = members,
                HasChildren = true
            });
        }

        return nodes;
    }

    private MemoryTreeNode CreateInstance(LoadedTree loaded, int index, bool isGrouped)
    {
        var entry = loaded.Entries[index];

        var address = GetAddressText(entry.Address);

        var name = entry.Name;

        if (isGrouped)
        {
            name = entry.ObjectName != entry.TypeName ? entry.ObjectName : address;
        }

        var detail = name == address ? string.Empty : address;

        return new MemoryTreeNode(name, detail, $"#{entry.Block}")
        {
            Entry = index,
            Block = entry.Block,
            Address = entry.Address,
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
                                             TraceHashPart? HashTablePart);

    private sealed record LoadedTree(List<TreeEntry> Entries, List<List<int>> Children, List<int> Roots, bool IsTruncated);
}

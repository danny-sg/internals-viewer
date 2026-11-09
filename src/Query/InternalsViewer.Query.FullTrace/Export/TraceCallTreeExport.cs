using System.Globalization;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Export;

internal static class TraceCallTreeExport
{
    public static Dictionary<CallTreeNode, int> Write(QueryExportWriter writer, CallTree tree, IEnumerable<CallTreeNode?> referenced)
    {
        var ids = new Dictionary<CallTreeNode, int>(ReferenceEqualityComparer.Instance);

        var nodes = new List<CallTreeNode>();

        AddSubtree(tree.Root, ids, nodes);

        foreach (var node in referenced)
        {
            AddDetached(node, ids, nodes);
        }

        var frames = new Dictionary<CallFrame, int>(ReferenceEqualityComparer.Instance);

        foreach (var node in nodes)
        {
            if (node.Frame is { } frame)
            {
                frames.TryAdd(frame, frames.Count);
            }
        }

        writer.WriteLines(TraceExportSection.FramesEntry, frames.Select(f => FrameOf(f.Key, f.Value)));

        writer.WriteLines(TraceExportSection.CallTreeEntry, nodes.Select(n => NodeOf(n, ids, frames)));

        return ids;
    }

    public static (CallTree Tree, List<CallTreeNode> Nodes)? Read(QueryExportReader reader)
    {
        if (!reader.HasEntry(TraceExportSection.CallTreeEntry))
        {
            return null;
        }

        var functions = new FrameFunctionLookup();

        var frames = new List<CallFrame>();

        foreach (var exported in reader.ReadLines<ExportedFrame>(TraceExportSection.FramesEntry))
        {
            CheckOrder(TraceExportSection.FramesEntry, exported.Id, frames.Count);

            frames.Add(FrameOf(exported, functions));
        }

        var tree = new CallTree();

        var nodes = new List<CallTreeNode>();

        foreach (var line in reader.ReadLines<ExportedNode>(TraceExportSection.CallTreeEntry))
        {
            CheckOrder(TraceExportSection.CallTreeEntry, line.Id, nodes.Count);

            var node = nodes.Count == 0
                ? tree.Root
                : new CallTreeNode
                {
                    Frame = line.Frame is { } frame ? frames[frame] : null,
                    Key = line.Key,
                    Order = line.Order
                };

            if (line.Parent is { } parentId)
            {
                var parent = nodes[parentId];

                node.Parent = parent;

                if (!line.Detached)
                {
                    parent.Children[node.Key] = node;
                }
            }

            node.CallCount = line.Calls;

            node.AllocatedBytes = line.AllocatedBytes;

            node.AllocationCount = line.Allocations;

            node.Locators = LocatorsOf(line.Locators);

            nodes.Add(node);
        }

        return (tree, nodes);
    }

    private static void AddSubtree(CallTreeNode top, Dictionary<CallTreeNode, int> ids, List<CallTreeNode> nodes)
    {
        var stack = new Stack<CallTreeNode>();

        stack.Push(top);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            ids.Add(node, nodes.Count);

            nodes.Add(node);

            foreach (var child in node.Children.Values.Reverse())
            {
                stack.Push(child);
            }
        }
    }

    private static void AddDetached(CallTreeNode? node, Dictionary<CallTreeNode, int> ids, List<CallTreeNode> nodes)
    {
        if (node is null || ids.ContainsKey(node))
        {
            return;
        }

        while (node.Parent is { } parent && IsAttached(node, parent))
        {
            node = parent;
        }

        if (ids.ContainsKey(node))
        {
            return;
        }

        AddDetached(node.Parent, ids, nodes);

        AddSubtree(node, ids, nodes);
    }

    private static bool IsAttached(CallTreeNode node, CallTreeNode parent)
        => parent.Children.TryGetValue(node.Key, out var child) && ReferenceEquals(child, node);

    private static void CheckOrder(string entry, int id, int expected)
    {
        if (id != expected)
        {
            throw new InvalidDataException($"{entry} is out of order: {id} where {expected} was expected");
        }
    }

    private static ExportedNode NodeOf(CallTreeNode node,
                                       Dictionary<CallTreeNode, int> ids,
                                       Dictionary<CallFrame, int> frames) => new()
    {
        Id = ids[node],
        Parent = node.Parent is { } parent ? ids[parent] : null,
        Detached = node.Parent is { } owner && !IsAttached(node, owner),
        Key = node.Key,
        Function = node.Frame?.QualifiedName,
        Order = node.Order,
        Frame = node.Frame is { } nodeFrame ? frames[nodeFrame] : null,
        Calls = node.CallCount,
        AllocatedBytes = node.AllocatedBytes,
        Allocations = node.AllocationCount,
        Locators = LocatorsOf(node.Locators)
    };

    private static ExportedFrame FrameOf(CallFrame frame, int id) => new()
    {
        Id = id,
        Module = frame.Module.Name,
        Address = frame.Address,
        Pdb = frame.Module.Pdb,
        Guid = frame.Module.Guid,
        Age = frame.Module.Age,
        Rva = frame.Rva,
        Instance = frame.InstanceAddress,
        Signature = frame.Function?.Signature,
        Symbol = frame.Function is { } function && frame.Offset is { } offset ? $"{function.Name}+0x{offset:X}" : frame.Function?.Name
    };

    private static CallFrame FrameOf(ExportedFrame exported, FrameFunctionLookup functions)
    {
        var frame = new CallFrame
        {
            Module = FrameModule.Get(exported.Module, exported.Pdb, exported.Guid, exported.Age),
            Address = exported.Address,
            Rva = exported.Rva,
            InstanceAddress = exported.Instance
        };

        if (exported.Symbol is { } symbol)
        {
            functions.Resolve(frame, symbol);

            frame.Function!.Signature ??= exported.Signature;
        }

        return frame;
    }

    private static ExportedLocators? LocatorsOf(LocatorSet? locators)
    {
        if (locators is null)
        {
            return null;
        }

        var ordered = locators.Ordered().ToList();

        List<string> pages = [.. ordered.Where(l => l.ValueType == LocatorValueType.Page)
                                        .Select(l => $"{l.Page.FileId}:{l.Page.PageId}")];

        List<string> rows = [.. ordered.Where(l => l.ValueType == LocatorValueType.Row)
                                       .Select(l => $"{l.Page.FileId}:{l.Page.PageId}:{l.Slot}")];

        List<ulong[]> objects = [.. ordered.Where(l => l.ValueType == LocatorValueType.ColumnStoreObject)
                                           .Select(l => new[] { l.Value, l.Detail })];

        return new ExportedLocators
        {
            Pages = pages.Count > 0 ? pages : null,
            Rows = rows.Count > 0 ? rows : null,
            Objects = objects.Count > 0 ? objects : null
        };
    }

    private static LocatorSet? LocatorsOf(ExportedLocators? exported)
    {
        if (exported is null)
        {
            return null;
        }

        var locators = new LocatorSet();

        foreach (var page in exported.Pages ?? [])
        {
            locators.Add(new Locator(0, LocatorValueType.Page, LocatorValue(page, 2)));
        }

        foreach (var row in exported.Rows ?? [])
        {
            locators.Add(new Locator(0, LocatorValueType.Row, LocatorValue(row, 3)));
        }

        foreach (var columnStoreObject in exported.Objects ?? [])
        {
            locators.Add(new Locator(0, LocatorValueType.ColumnStoreObject, columnStoreObject[0], columnStoreObject[1]));
        }

        return locators;
    }

    private static ulong LocatorValue(string text, int parts)
    {
        var values = text.Split(':');

        if (values.Length != parts)
        {
            throw new InvalidDataException($"'{text}' is not a page or row id");
        }

        var fileId = (ushort)short.Parse(values[0], CultureInfo.InvariantCulture);

        var pageId = uint.Parse(values[1], CultureInfo.InvariantCulture);

        var slot = parts == 3 ? ulong.Parse(values[2], CultureInfo.InvariantCulture) : 0;

        return (slot << 48) | ((ulong)fileId << 32) | pageId;
    }
}

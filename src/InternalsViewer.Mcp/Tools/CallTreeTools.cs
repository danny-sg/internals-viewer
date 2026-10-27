using System.ComponentModel;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class CallTreeTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Document id from list_documents; the active document when omitted";

    private const string UnknownCategory = "Unknown";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "calltree", ReadOnly = true)]
    [Description("Print, search or rank the call tree of engine functions: the subtree under a node or under a plan operator's entry " +
                 "frames, the nodes whose function name matches a regex with their call paths, the call path to a node, or the hottest " +
                 "nodes by calls or allocations")]
    public async Task<string> CallTree([Description("Call tree name: fulltrace (the default, falling back to events) or events")]
                                       string tree = "fulltrace",
                                       [Description("Start at this call tree node id")] int? root = null,
                                       [Description("Start at the entry frames of this plan node id and stop at its exit frames")]
                                       int? @operator = null,
                                       [Description("Levels to print below the root; deeper subtrees are marked")] int depth = 6,
                                       [Description("Hide subtrees with fewer calls including children")] int minCalls = 0,
                                       [Description("Show calls including children on each line")] bool inclusive = false,
                                       [Description("Maximum lines")] int limit = 300,
                                       [Description("Regex over function names; lists the matches with their call path, " +
                                                    "scoped by operator or root")]
                                       string? find = null,
                                       [Description("Width of the pages shown by path")] int width = 100,
                                       [Description("Print the call path from the root to this node id")] int? path = null,
                                       [Description("Call path levels shown by find")] int pathDepth = 8,
                                       [Description("List the top N nodes, scoped by operator or root")] int? hot = null,
                                       [Description("Ranking for hot: calls, alloc or inclusive-alloc")] string by = "calls",
                                       [Description(DocumentDescription)] string? document = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var context = scope.Context;

        var builder = new StringBuilder();

        if (path is { } pathId)
        {
            WritePath(builder, context, GetNode(context, pathId), width);

            return builder.ToString();
        }

        var (roots, stop, scoped) = SelectRoots(scope, tree, root, @operator, builder);

        if (roots.Count == 0)
        {
            return builder.ToString();
        }

        var within = scoped ? GetSubtree(roots, stop) : null;

        var totals = new SubtreeTotals();

        if (find is not null)
        {
            WriteMatches(builder, context, GetCandidates(context, within), find, scoped, limit, pathDepth);
        }
        else if (hot is { } top)
        {
            WriteHot(builder, context, GetCandidates(context, within), totals, top, by);
        }
        else
        {
            WriteTree(builder, context, roots, stop, totals, depth, minCalls, inclusive, limit);
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "functions", ReadOnly = true)]
    [Description("Totals per function across the whole call tree: nodes, calls, allocations and pages touched, with the app's engine " +
                 "category and operator badge")]
    public async Task<string> Functions([Description("Functions to list")] int top = 40,
                                        [Description("Ranking: calls, alloc, nodes or pages")] string by = "calls",
                                        [Description("Regex over function names")] string? grep = null,
                                        [Description("Only functions in this category, for example BTreeNavigation or Latching")]
                                        string? category = null,
                                        [Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var totals = GetFunctionTotals(scope.Context, grep);

        if (category is not null)
        {
            var wanted = NormaliseCategory(category);

            totals = totals.Where(t => NormaliseCategory(t.Category) == wanted).ToList();
        }

        Func<FunctionTotal, long> key = by switch
        {
            "calls" => t => t.Calls,
            "alloc" => t => t.Allocated,
            "nodes" => t => t.Nodes,
            "pages" => t => t.Pages.Count,
            _ => throw new McpException($"'{by}' is not a ranking. Use calls, alloc, nodes or pages.")
        };

        var table = new TextTable("Function", "Nodes", "Calls", "Alloc", "Allocs", "Pages", "Category", "Operator");

        foreach (var total in totals.OrderByDescending(key).Take(top))
        {
            table.Add(total.Name,
                      total.Nodes,
                      total.Calls,
                      Format.Bytes(total.Allocated),
                      total.Allocations,
                      total.Pages.Count,
                      total.Category,
                      total.Operator);
        }

        return table.ToString();
    }

    [McpServerTool(Name = "categories", ReadOnly = true)]
    [Description("Calls and allocations per engine category (B-Tree Navigation, Latching, etc.) across the call tree, with the most " +
                 "called functions of each")]
    public async Task<string> Categories([Description("Ranking: calls, alloc or nodes")] string by = "calls",
                                         [Description("Most called functions to show per category")] int examples = 3,
                                         [Description("Width of the examples column")] int width = 110,
                                         [Description(DocumentDescription)] string? document = null,
                                         CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var categories = new Dictionary<string, CategoryTotal>(StringComparer.Ordinal);

        foreach (var total in GetFunctionTotals(scope.Context, null))
        {
            if (!categories.TryGetValue(total.Category, out var entry))
            {
                entry = new CategoryTotal { Name = total.Category };

                categories[total.Category] = entry;
            }

            entry.Nodes += total.Nodes;

            entry.Calls += total.Calls;

            entry.Allocated += total.Allocated;

            entry.Allocations += total.Allocations;

            entry.CallsByFunction[total.Name] = entry.CallsByFunction.GetValueOrDefault(total.Name) + total.Calls;
        }

        Func<CategoryTotal, long> key = by switch
        {
            "calls" => c => c.Calls,
            "alloc" => c => c.Allocated,
            "nodes" => c => c.Nodes,
            _ => throw new McpException($"'{by}' is not a ranking. Use calls, alloc or nodes.")
        };

        var table = new TextTable("Category", "Nodes", "Calls", "Alloc", "Allocs", "Most called");

        foreach (var entry in categories.Values.OrderByDescending(key))
        {
            var top = entry.CallsByFunction.OrderByDescending(f => f.Value)
                           .Take(examples)
                           .Select(f => $"{WithoutModule(f.Key)} x{Format.Number(f.Value)}");

            table.Add(entry.Name,
                      entry.Nodes,
                      entry.Calls,
                      Format.Bytes(entry.Allocated),
                      entry.Allocations,
                      Format.Truncate(string.Join(", ", top), width));
        }

        var builder = new StringBuilder();

        table.Write(builder);

        builder.AppendLine();

        builder.AppendLine("Categories follow the app's symbol mapping rules. Extended Events, Tracing, SQL OS Scheduling and " +
                           "Synchronisation,");

        builder.AppendLine("I/O Infrastructure and System are infrastructure the app hides by default.");

        return builder.ToString();
    }

    [McpServerTool(Name = "locators", ReadOnly = true)]
    [Description("Pages and rows seen as call arguments and which functions touched them: a summary per page, the nodes that touched " +
                 "one page, or the pages, rows and columnstore objects on one call tree node")]
    public async Task<string> Locators([Description("Page address file:page to drill into")] string? page = null,
                                       [Description("List the pages, rows and columnstore objects on this call tree node id")]
                                       int? node = null,
                                       [Description("Pages to list in the summary")] int limit = 50,
                                       [Description("Width of the functions column")] int width = 100,
                                       [Description("Call path levels shown for a page's nodes")] int pathDepth = 6,
                                       [Description(DocumentDescription)] string? document = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var context = scope.Context;

        var builder = new StringBuilder();

        if (node is { } nodeId)
        {
            WriteNodeLocators(builder, context, GetNode(context, nodeId));

            return builder.ToString();
        }

        var nodesByPage = new Dictionary<PageAddress, List<CallStackNode>>();

        var slotsByPage = new Dictionary<PageAddress, SortedSet<int>>();

        foreach (var candidate in context.Nodes.GetNodes())
        {
            foreach (var locator in GetLocators(candidate))
            {
                if (locator.ValueType == LocatorValueType.Page)
                {
                    GetOrAdd(nodesByPage, locator.Page).Add(candidate);
                }
                else if (locator.ValueType == LocatorValueType.Row)
                {
                    GetOrAdd(slotsByPage, locator.Page).Add(locator.Slot);
                }
            }
        }

        if (page is not null)
        {
            var pageAddress = Parse.PageAddress(page);

            var slots = slotsByPage.GetValueOrDefault(pageAddress) ?? [];

            var touchedSlots = SlotList(slots.Take(50));

            builder.AppendLine($"page {pageAddress.FileId}:{pageAddress.PageId}: {slots.Count} row slots touched {touchedSlots}");

            foreach (var touched in nodesByPage.GetValueOrDefault(pageAddress) ?? [])
            {
                builder.AppendLine($"  {Format.NodeLine(context, touched)}");

                builder.AppendLine($"    called from: {CalledFrom(context, touched, pathDepth)}");
            }

            return builder.ToString();
        }

        builder.AppendLine($"{nodesByPage.Count} distinct pages appear as call arguments, {slotsByPage.Count} with row slots");

        var table = new TextTable("Page", "Nodes", "Slots", "Functions");

        foreach (var (pageAddress, touched) in nodesByPage.OrderByDescending(p => p.Value.Count).Take(limit))
        {
            var functions = touched.GroupBy(context.GetFunctionName)
                                   .OrderByDescending(g => g.Count())
                                   .Take(3)
                                   .Select(g => $"{g.Key} x{g.Count()}");

            table.Add($"{pageAddress.FileId}:{pageAddress.PageId}",
                      touched.Count,
                      slotsByPage.GetValueOrDefault(pageAddress)?.Count ?? 0,
                      Format.Truncate(string.Join(", ", functions), width));
        }

        table.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "frames", ReadOnly = true)]
    [Description("The resolved call stack frames (symbols) behind the call tree: module, function, address, RVA, instance and signature")]
    public async Task<string> Frames([Description("Regex over function names")] string? grep = null,
                                     [Description("Also show each frame's signature")] bool signatures = false,
                                     [Description("Frames to list")] int limit = 50,
                                     [Description(DocumentDescription)] string? document = null,
                                     CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var context = scope.Context;

        var pattern = grep is null ? null : Parse.Pattern(grep);

        var seen = new HashSet<CallstackFrame>(ReferenceEqualityComparer.Instance);

        var builder = new StringBuilder();

        var count = 0;

        foreach (var node in context.Nodes.GetNodes())
        {
            if (node.Frame is not { } frame || !seen.Add(frame))
            {
                continue;
            }

            var name = context.GetFunctionName(node);

            if (pattern is not null && !pattern.IsMatch(name))
            {
                continue;
            }

            var instance = frame.InstanceAddress != 0 ? $" instance {Format.Hex(frame.InstanceAddress)}" : string.Empty;

            var signature = signatures && frame.Signature is { Length: > 0 } text ? $"  {text}" : string.Empty;

            builder.AppendLine($"{name}  address {Format.Hex(frame.Address)} rva {Format.Hex(frame.Rva)}{instance}{signature}");

            count++;

            if (count >= limit)
            {
                builder.AppendLine("(more; use grep or raise limit)");

                break;
            }
        }

        return builder.ToString();
    }

    private static (List<CallStackNode> Roots, HashSet<CallStackNode> Stop, bool Scoped) SelectRoots(McpDocumentScope scope,
                                                                                                     string tree,
                                                                                                     int? root,
                                                                                                     int? operatorNodeId,
                                                                                                     StringBuilder builder)
    {
        var stop = new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

        if (operatorNodeId is { } nodeId)
        {
            var roots = new List<CallStackNode>();

            foreach (var operatorEvent in scope.Result.EngineEvents.OfType<ExecutionOperatorEvent>()
                                               .Where(o => o.PlanNodeIdentifier?.NodeId == nodeId))
            {
                roots.AddRange(operatorEvent.EntryFrames);

                stop.UnionWith(operatorEvent.ExitFrames);

                builder.AppendLine($"Node {nodeId} {operatorEvent.OperatorDescription}: {roots.Count} entry frames, " +
                                   $"cut at {stop.Count} exit frames");
            }

            if (roots.Count == 0)
            {
                builder.AppendLine($"no operator event for plan node {nodeId}, or it has no entry frames");
            }

            return (roots, stop, true);
        }

        if (root is { } rootId)
        {
            return ([GetNode(scope.Context, rootId)], stop, true);
        }

        if (GetTreeRoot(scope, tree) is { } treeRoot)
        {
            return ([treeRoot], stop, false);
        }

        builder.AppendLine("no call tree in the run");

        return ([], stop, false);
    }

    private static CallStackNode? GetTreeRoot(McpDocumentScope scope, string tree)
    {
        var traceRoot = scope.Trace?.CallStack.Root;

        var eventsRoot = scope.Result.CallStackTree?.Root;

        return string.Equals(tree, "events", StringComparison.OrdinalIgnoreCase) ? eventsRoot ?? traceRoot : traceRoot ?? eventsRoot;
    }

    private static HashSet<CallStackNode> GetSubtree(List<CallStackNode> roots, HashSet<CallStackNode> stop)
    {
        var subtree = new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

        var stack = new Stack<CallStackNode>(roots);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            if (!subtree.Add(node) || stop.Contains(node))
            {
                continue;
            }

            foreach (var child in node.Children.Values)
            {
                stack.Push(child);
            }
        }

        return subtree;
    }

    private static IEnumerable<CallStackNode> GetCandidates(DocumentContext context, HashSet<CallStackNode>? within)
        => within is null ? context.Nodes.GetNodes() : context.Nodes.GetNodes().Where(within.Contains);

    private static void WritePath(StringBuilder builder, DocumentContext context, CallStackNode target, int width)
    {
        foreach (var node in target.Ancestors().Reverse())
        {
            var pages = GetPages(node).ToList();

            var extra = pages.Count > 0 ? $"  pages: {Format.Truncate(Format.CompactPages(pages), width)}" : string.Empty;

            builder.AppendLine($"{Indent(node.Ancestors().Count() - 1)}{Format.NodeLine(context, node)}{extra}");
        }
    }

    private static void WriteMatches(StringBuilder builder,
                                     DocumentContext context,
                                     IEnumerable<CallStackNode> candidates,
                                     string find,
                                     bool scoped,
                                     int limit,
                                     int pathDepth)
    {
        var pattern = Parse.Pattern(find);

        var matches = candidates.Where(n => pattern.IsMatch(context.GetFunctionName(n))).OrderByDescending(n => n.Calls).ToList();

        builder.AppendLine($"{matches.Count} matching nodes{(scoped ? " in the selected subtree" : string.Empty)}, most calls first");

        foreach (var node in matches.Take(limit))
        {
            builder.AppendLine(Format.NodeLine(context, node));

            builder.AppendLine($"    called from: {Format.CallPath(context, node, pathDepth)}");
        }

        if (matches.Count > limit)
        {
            builder.AppendLine($"(+{matches.Count - limit} more; raise limit or scope with operator or root)");
        }
    }

    private static void WriteHot(StringBuilder builder,
                                 DocumentContext context,
                                 IEnumerable<CallStackNode> candidates,
                                 SubtreeTotals totals,
                                 int top,
                                 string by)
    {
        Func<CallStackNode, long> key = by switch
        {
            "calls" => n => n.Calls,
            "alloc" => n => n.AllocatedBytes,
            "inclusive-alloc" => n => totals.Get(n).Allocated,
            _ => throw new McpException($"'{by}' is not a ranking. Use calls, alloc or inclusive-alloc.")
        };

        foreach (var node in candidates.OrderByDescending(key).Take(top))
        {
            var inclusive = totals.Get(node);

            var allocated = $"{Format.Bytes(inclusive.Allocated)}/{Format.Number(inclusive.Allocations)}";

            builder.AppendLine($"{Format.NodeLine(context, node)}  inclusive alloc={allocated}");
        }
    }

    private static void WriteTree(StringBuilder builder,
                                  DocumentContext context,
                                  List<CallStackNode> roots,
                                  HashSet<CallStackNode> stop,
                                  SubtreeTotals totals,
                                  int depth,
                                  int minCalls,
                                  bool inclusive,
                                  int limit)
    {
        var printed = 0;

        var cut = 0;

        foreach (var root in roots)
        {
            var stack = new Stack<(CallStackNode Node, int Level)>();

            stack.Push((root, 0));

            while (stack.Count > 0 && printed < limit)
            {
                var (node, level) = stack.Pop();

                var subtree = totals.Get(node);

                if (minCalls > 0 && subtree.Calls < minCalls)
                {
                    continue;
                }

                var isStop = stop.Contains(node);

                var marker = isStop ? " [exit]" : string.Empty;

                if (level >= depth && node.Children.Count > 0 && !isStop)
                {
                    marker += $"  (+{node.Children.Count} children below depth {depth})";

                    cut++;
                }

                var inclusiveCalls = inclusive ? $"  inclusive calls={Format.Number(subtree.Calls)}" : string.Empty;

                builder.AppendLine($"{Indent(level)}{Format.NodeLine(context, node)}{marker}{inclusiveCalls}");

                printed++;

                if (level >= depth || isStop)
                {
                    continue;
                }

                foreach (var child in node.Children.Values.OrderByDescending(c => c.Order))
                {
                    stack.Push((child, level + 1));
                }
            }
        }

        if (printed >= limit)
        {
            builder.AppendLine($"(stopped after {limit} lines; raise limit, lower depth or use minCalls)");
        }

        if (cut > 0)
        {
            builder.AppendLine($"({cut} subtrees continue below depth {depth}; use root <id> with depth N to open one)");
        }
    }

    private static void WriteNodeLocators(StringBuilder builder, DocumentContext context, CallStackNode node)
    {
        builder.AppendLine(Format.NodeLine(context, node));

        var locators = GetLocators(node).ToList();

        var pages = locators.Where(l => l.ValueType == LocatorValueType.Page).Select(l => l.Page).ToList();

        var rows = locators.Where(l => l.ValueType == LocatorValueType.Row).ToList();

        var objects = locators.Where(l => l.ValueType == LocatorValueType.ColumnStoreObject).ToList();

        if (pages.Count > 0)
        {
            builder.AppendLine($"  pages ({pages.Count}): {Format.CompactPages(pages)}");
        }

        if (rows.Count > 0)
        {
            builder.AppendLine($"  rows ({rows.Count}):");

            foreach (var group in rows.GroupBy(r => r.Page).OrderBy(g => g.Key.FileId).ThenBy(g => g.Key.PageId))
            {
                var slots = SlotList(group.Select(r => (int)r.Slot).Distinct().Order());

                builder.AppendLine($"    {group.Key.FileId}:{group.Key.PageId} slots {slots}");
            }
        }

        if (objects.Count > 0)
        {
            builder.AppendLine($"  columnstore objects ({objects.Count}):");

            foreach (var locator in objects)
            {
                builder.AppendLine($"    {locator}");
            }
        }

        if (locators.Count == 0)
        {
            builder.AppendLine("  no locators on this node");
        }
    }

    private static List<FunctionTotal> GetFunctionTotals(DocumentContext context, string? grep)
    {
        var pattern = grep is null ? null : Parse.Pattern(grep);

        var totals = new Dictionary<string, FunctionTotal>(StringComparer.Ordinal);

        foreach (var node in context.Nodes.GetNodes())
        {
            if (node.IsRoot)
            {
                continue;
            }

            var name = context.GetFunctionName(node);

            if (pattern is not null && !pattern.IsMatch(name))
            {
                continue;
            }

            if (!totals.TryGetValue(name, out var total))
            {
                total = new FunctionTotal
                {
                    Name = name,
                    Category = GetCategory(node),
                    Operator = node.Frame?.Resolved?.Iterator ?? string.Empty
                };

                totals[name] = total;
            }

            total.Nodes++;

            total.Calls += node.Calls;

            total.Allocated += node.AllocatedBytes;

            total.Allocations += node.Allocations;

            total.Pages.UnionWith(GetPages(node));
        }

        return [.. totals.Values];
    }

    private static string GetCategory(CallStackNode node)
        => node.Frame?.Resolved?.SymbolCategory.GetCategoryMetadata()?.Name ?? UnknownCategory;

    private static string NormaliseCategory(string category) => category.Replace(" ", string.Empty).ToLowerInvariant();

    private static string WithoutModule(string name)
    {
        var separator = name.IndexOf('!');

        return separator >= 0 ? name[(separator + 1)..] : name;
    }

    private static string CalledFrom(DocumentContext context, CallStackNode node, int pathDepth)
        => string.Join(" < ", node.Ancestors().Skip(1).Take(pathDepth).Select(context.GetFunctionName));

    private static string SlotList(IEnumerable<int> slots) => $"[{string.Join(", ", slots)}]";

    private static IEnumerable<Locator> GetLocators(CallStackNode node) => node.Locators?.Ordered() ?? [];

    private static IEnumerable<PageAddress> GetPages(CallStackNode node)
        => GetLocators(node).Where(l => l.ValueType == LocatorValueType.Page).Select(l => l.Page);

    private static CallStackNode GetNode(DocumentContext context, int id)
        => context.Nodes.GetNode(id) ?? throw new McpException($"No call tree node {id}.");

    private static TValue GetOrAdd<TValue>(Dictionary<PageAddress, TValue> dictionary, PageAddress page) where TValue : new()
    {
        if (!dictionary.TryGetValue(page, out var value))
        {
            value = new TValue();

            dictionary[page] = value;
        }

        return value;
    }

    private static string Indent(int level) => new(' ', Math.Max(level, 0) * 2);

    private sealed class FunctionTotal
    {
        public required string Name { get; init; }

        public required string Category { get; init; }

        public required string Operator { get; init; }

        public int Nodes { get; set; }

        public long Calls { get; set; }

        public long Allocated { get; set; }

        public long Allocations { get; set; }

        public HashSet<PageAddress> Pages { get; } = [];
    }

    private sealed class CategoryTotal
    {
        public required string Name { get; init; }

        public long Nodes { get; set; }

        public long Calls { get; set; }

        public long Allocated { get; set; }

        public long Allocations { get; set; }

        public Dictionary<string, long> CallsByFunction { get; } = new(StringComparer.Ordinal);
    }

    private sealed class SubtreeTotals
    {
        private Dictionary<CallStackNode, (long Calls, long Allocated, long Allocations)> Cache { get; } =
            new(ReferenceEqualityComparer.Instance);

        public (long Calls, long Allocated, long Allocations) Get(CallStackNode root)
        {
            if (Cache.TryGetValue(root, out var cached))
            {
                return cached;
            }

            var stack = new Stack<(CallStackNode Node, bool Expanded)>();

            stack.Push((root, false));

            while (stack.Count > 0)
            {
                var (node, expanded) = stack.Pop();

                if (Cache.ContainsKey(node))
                {
                    continue;
                }

                if (!expanded)
                {
                    stack.Push((node, true));

                    foreach (var child in node.Children.Values)
                    {
                        if (!Cache.ContainsKey(child))
                        {
                            stack.Push((child, false));
                        }
                    }

                    continue;
                }

                var calls = node.Calls;

                var allocated = node.AllocatedBytes;

                var allocations = node.Allocations;

                foreach (var child in node.Children.Values)
                {
                    var childTotals = Cache[child];

                    calls += childTotals.Calls;

                    allocated += childTotals.Allocated;

                    allocations += childTotals.Allocations;
                }

                Cache[node] = (calls, allocated, allocations);
            }

            return Cache[root];
        }
    }
}

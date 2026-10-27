using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class RunTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private static XNamespace ShowPlan { get; } = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    private static JsonSerializerOptions Indented { get; } = new() { WriteIndented = true };

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "query_summary", ReadOnly = true)]
    [Description("What the run was: server, query, options, plans, Full Trace sizes, event counts by type and messages. Run this first")]
    public async Task<string> Summary([Description(DocumentDescription)] string? document = null,
                                      CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var run = scope.Run;

        var result = scope.Result;

        var builder = new StringBuilder();

        builder.AppendLine($"Document:      {scope.Query.TabId} ({scope.Query.Title})");

        builder.AppendLine($"Server:        {run.ServerName}");

        builder.AppendLine($"Database:      {run.DatabaseName}");

        builder.AppendLine($"Session:       {result.SessionId}");

        builder.AppendLine($"Success:       {result.IsSuccess}  {result.Message}".TrimEnd());

        builder.AppendLine($"Rows returned: {result.RowCount} (result rows are not available here, only the columns)");

        if (result is { CropStartUs: { } cropStart, CropEndUs: { } cropEnd })
        {
            builder.AppendLine($"Query window:  {Format.Milliseconds(cropStart)} ms to {Format.Milliseconds(cropEnd)} ms " +
                               "capture-relative " +
                               $"({Format.Milliseconds(cropEnd - cropStart)} ms)");
        }

        builder.AppendLine();

        builder.AppendLine("Query:");

        foreach (var line in run.SqlText.Split('\n'))
        {
            builder.AppendLine($"  {line.TrimEnd('\r')}");
        }

        builder.AppendLine();

        builder.AppendLine("Query options: " + string.Join(", ", Options(run.QueryOptions)));

        builder.AppendLine("Event options: " + string.Join(", ", Options(run.EventOptions)));

        var categories = run.EventOptions.IncludeLockModeCategories;

        builder.AppendLine($"Lock categories captured: {(categories.Count > 0 ? string.Join(", ", categories) : "none")}");

        if (scope.Trace is { } trace)
        {
            builder.AppendLine("Full Trace:    yes");

            foreach (var (component, items, bytes) in trace.GetComponentSizes())
            {
                var size = bytes is { } byteCount ? $"  {Format.Bytes(byteCount)}" : string.Empty;

                builder.AppendLine($"  {component,-22} {Format.Number(items),14}{size}");
            }
        }
        else
        {
            builder.AppendLine("Full Trace:    no");
        }

        builder.AppendLine();

        for (var index = 0; index < result.ResultSets.Count; index++)
        {
            var columns = result.ResultSets[index].Columns.Select(c => $"{c.Name} ({c.TypeName ?? c.ClrType.Name})");

            builder.AppendLine($"Result set {index}: {string.Join(", ", columns)}");
        }

        builder.AppendLine();

        builder.AppendLine("Plans:");

        for (var index = 0; index < result.ExecutionPlans.Count; index++)
        {
            var plan = result.ExecutionPlans[index];

            builder.AppendLine($"  plan {index} handle {plan.PlanHandleId} internal={plan.IsInternalPlan} {plan.NodesById.Count} nodes");
        }

        builder.AppendLine();

        var all = scope.Context.Events.GetEvents(includeChildren: true);

        builder.AppendLine("Call tree:");

        builder.AppendLine($"  {scope.Context.Nodes.Count} nodes, {all.Length} events, {result.LogRecords.Count} log records");

        builder.AppendLine();

        builder.AppendLine("Events by type (top-level / all):");

        foreach (var group in all.GroupBy(Format.EventType).OrderByDescending(g => g.Count()))
        {
            builder.AppendLine($"  {group.Key,-26} {group.Count(scope.Context.Events.IsTopLevel),8} / {group.Count()}");
        }

        builder.AppendLine();

        builder.AppendLine("Messages:");

        foreach (var message in run.Messages)
        {
            builder.AppendLine($"  {message}");
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "query_plan", ReadOnly = true)]
    [Description("The execution plan as an indented operator tree with estimated and actual rows, warnings and the memory grant")]
    public async Task<string> Plan([Description("Plan index from query_summary")] int index = 0,
                                   [Description("Predicates, seek predicates, warnings and batch info per operator")] bool detail = false,
                                   [Description("Append the batch (columnstore) info per node as JSON")] bool batch = false,
                                   [Description("Return the raw plan XML instead")] bool xml = false,
                                   [Description(DocumentDescription)] string? document = null,
                                   CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var plans = scope.Result.ExecutionPlans;

        if (plans.Count == 0)
        {
            return "no plans in the run";
        }

        if (index < 0 || index >= plans.Count)
        {
            throw new McpException($"No plan {index}; the run has {plans.Count} plans.");
        }

        var plan = plans[index];

        if (xml)
        {
            return plan.Xml ?? string.Empty;
        }

        var builder = new StringBuilder();

        if (string.IsNullOrWhiteSpace(plan.Xml))
        {
            builder.AppendLine("The plan has no XML; operators from the parsed plan:");

            foreach (var root in plan.Root)
            {
                WritePlanNode(builder, root, 0, detail);
            }
        }
        else
        {
            WritePlanXml(builder, XDocument.Parse(plan.Xml).Root!, plan, detail);
        }

        var batchInfo = plan.NodesById.Where(n => n.Value.BatchInfo is not null)
                            .ToDictionary(n => n.Key.ToString(CultureInfo.InvariantCulture), n => n.Value.BatchInfo);

        if (batch && batchInfo.Count > 0)
        {
            builder.AppendLine();

            builder.AppendLine("Batch info:");

            builder.AppendLine(JsonSerializer.Serialize(batchInfo, Indented));
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "query_operators", ReadOnly = true)]
    [Description("Plan operators as executed: timing, first row, rows, threads and entry frames, with frames and instances on request")]
    public async Task<string> Operators([Description("Also list entry and exit frames, instances, lifetimes, threads")] bool frames = false,
                                        [Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        scope.RequireOperatorFrames();

        var operators = scope.Context.Events.GetEvents(includeChildren: true)
                             .OfType<ExecutionOperatorEvent>()
                             .OrderBy(o => o.PlanNodeIdentifier?.NodeId ?? -1)
                             .ToList();

        var builder = new StringBuilder();

        var table = new TextTable("Node", "Parent", "Operator", "Logical", "Target", "Start", "Dur ms", "First row", "Rows", "Thr",
                                  "Entry", "Inst");

        foreach (var operatorEvent in operators)
        {
            table.Add(operatorEvent.PlanNodeIdentifier?.NodeId,
                      operatorEvent.ParentNodeId,
                      operatorEvent.OperatorDescription,
                      operatorEvent.LogicalOperator,
                      Target(operatorEvent),
                      Format.Relative(operatorEvent.TimeUs, scope.CropStartUs),
                      Format.Milliseconds(operatorEvent.DurationUs),
                      Format.Relative(operatorEvent.EmitStartUs, scope.CropStartUs),
                      operatorEvent.RowsProcessed,
                      operatorEvent.Threads.Count,
                      operatorEvent.EntryFrames.Count,
                      operatorEvent.Instances.Count);
        }

        table.Write(builder);

        if (!frames)
        {
            return builder.ToString();
        }

        foreach (var operatorEvent in operators)
        {
            builder.AppendLine();

            builder.AppendLine($"Node {operatorEvent.PlanNodeIdentifier?.NodeId} {operatorEvent.OperatorDescription}:");

            foreach (var frame in operatorEvent.EntryFrames)
            {
                builder.AppendLine($"  entry {Format.NodeLine(scope.Context, frame)}");
            }

            foreach (var frame in operatorEvent.ExitFrames)
            {
                builder.AppendLine($"  exit  {Format.NodeLine(scope.Context, frame)}");
            }

            foreach (var instance in operatorEvent.Instances)
            {
                builder.AppendLine($"  iterator instance {Format.Hex(instance)}");
            }

            if (scope.Trace is { } trace)
            {
                foreach (var lifetime in trace.Lifetimes.Where(l => ReferenceEquals(l.Operator, operatorEvent)))
                {
                    builder.AppendLine($"  lifetime thread {lifetime.Thread} from {Format.Position(lifetime.Start)} " +
                                       $"to {Format.Position(lifetime.End)} " +
                                       $"({lifetime.Calls.Count} calls, constructed {Format.Position(lifetime.Constructed)}, " +
                                       $"destroyed {Format.Position(lifetime.Destroyed)})");
                }
            }

            foreach (var thread in operatorEvent.Threads)
            {
                builder.AppendLine($"  thread {thread.ThreadId}: start {Format.Relative(thread.StartUs, scope.CropStartUs)} ms, " +
                                   $"{Format.Milliseconds(thread.DurationUs)} ms, {thread.RowsProcessed} rows");
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Options(object options)
    {
        foreach (var property in options.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                continue;
            }

            yield return $"{property.Name}={property.GetValue(options) ?? "null"}";
        }
    }

    private static string Target(ExecutionOperatorEvent operatorEvent)
    {
        var target = operatorEvent.OperatorTableName;

        return operatorEvent.OperatorIndexName.Length > 0 ? $"{target}.{operatorEvent.OperatorIndexName}" : target;
    }

    private static void WritePlanXml(StringBuilder builder, XElement root, ExecutionPlan plan, bool detail)
    {
        if (root.Name.LocalName == "event")
        {
            string[] wanted = ["cpu_time", "duration", "dop", "estimated_rows", "granted_memory_kb", "used_memory_kb"];

            foreach (var data in root.Elements("data"))
            {
                var name = (string?)data.Attribute("name");

                if (name is not null && wanted.Contains(name))
                {
                    builder.AppendLine($"{name}: {data.Element("value")?.Value}");
                }
            }
        }

        foreach (var statement in root.Descendants(ShowPlan + "StmtSimple"))
        {
            builder.AppendLine($"Statement: {Attribute(statement, "StatementType")} est rows {Attribute(statement, "StatementEstRows")} " +
                               $"cost {Attribute(statement, "StatementSubTreeCost")} hash {Attribute(statement, "QueryHash")} " +
                               $"plan hash {Attribute(statement, "QueryPlanHash")}");

            if (Attribute(statement, "StatementText") is { Length: > 0 } text)
            {
                builder.AppendLine($"  {text}");
            }
        }

        foreach (var queryPlan in root.Descendants(ShowPlan + "QueryPlan"))
        {
            builder.AppendLine($"QueryPlan: DOP {Attribute(queryPlan, "DegreeOfParallelism")} " +
                               $"compile {Attribute(queryPlan, "CompileTime")} ms " +
                               $"cached size {Attribute(queryPlan, "CachedPlanSize")} KB");

            if (queryPlan.Element(ShowPlan + "MemoryGrantInfo") is { } grant)
            {
                builder.AppendLine("  memory grant: " + string.Join(", ", grant.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}")));
            }

            if (queryPlan.Element(ShowPlan + "QueryTimeStats") is { } stats)
            {
                builder.AppendLine($"  elapsed {Attribute(stats, "ElapsedTime")} ms, cpu {Attribute(stats, "CpuTime")} ms");
            }

            if (queryPlan.Element(ShowPlan + "WaitStats") is { } waits)
            {
                var items = waits.Elements()
                                 .Select(w => $"{Attribute(w, "WaitType")} {Attribute(w, "WaitTimeMs")} ms x{Attribute(w, "WaitCount")}");

                builder.AppendLine("  waits: " + string.Join(", ", items));
            }

            foreach (var warnings in queryPlan.Descendants(ShowPlan + "Warnings"))
            {
                builder.AppendLine("  warnings: " + string.Join(", ", warnings.Elements().Select(Warning)));
            }

            builder.AppendLine();

            foreach (var relOp in RelOpsUnder(queryPlan))
            {
                WriteRelOp(builder, relOp, 0, plan, detail);
            }
        }
    }

    private static void WriteRelOp(StringBuilder builder, XElement relOp, int depth, ExecutionPlan plan, bool detail)
    {
        var nodeId = Attribute(relOp, "NodeId");

        long actualRows = 0;

        long actualReads = 0;

        long executions = 0;

        foreach (var counters in OwnElements(relOp, "RunTimeCountersPerThread"))
        {
            actualRows += Number(counters, "ActualRows");

            actualReads += Number(counters, "ActualLogicalReads");

            executions += Number(counters, "ActualExecutions");
        }

        var mode = Attribute(relOp, "ActualExecutionMode") ?? Attribute(relOp, "EstimatedExecutionMode") ?? string.Empty;

        var parallel = Attribute(relOp, "Parallel") == "1" ? "parallel" : string.Empty;

        var indent = new string(' ', depth * 2);

        builder.AppendLine($"{indent}[{nodeId}] {Attribute(relOp, "PhysicalOp")} ({Attribute(relOp, "LogicalOp")}) {PlanObject(relOp)} " +
                           $"est {Attribute(relOp, "EstimateRows")} actual {actualRows} rows, {executions} exec, " +
                           $"{actualReads} logical reads, " +
                           $"{mode} {parallel}".TrimEnd());

        if (detail)
        {
            foreach (var predicate in OwnElements(relOp, "Predicate"))
            {
                builder.AppendLine($"{indent}    predicate: {TextOf(predicate)}");
            }

            foreach (var seek in OwnElements(relOp, "SeekPredicates"))
            {
                builder.AppendLine($"{indent}    seek: {TextOf(seek)}");
            }

            foreach (var keys in OwnElements(relOp, "HashKeysBuild"))
            {
                builder.AppendLine($"{indent}    hash build keys: {Columns(keys)}");
            }

            foreach (var keys in OwnElements(relOp, "HashKeysProbe"))
            {
                builder.AppendLine($"{indent}    hash probe keys: {Columns(keys)}");
            }

            foreach (var warnings in OwnElements(relOp, "Warnings"))
            {
                builder.AppendLine($"{indent}    warnings: [{string.Join(", ", warnings.Elements().Select(w => w.Name.LocalName))}]");
            }

            if (int.TryParse(nodeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && plan.NodesById.TryGetValue(id, out var planNode)
                && planNode.BatchInfo is { } info
                && (info.BatchCount > 0 || info.SegmentReads > 0 || info.SegmentSkips > 0 || info.SegmentScans.Count > 0))
            {
                builder.AppendLine($"{indent}    batch: {info.BatchCount} batches, {info.SegmentReads} segment reads, " +
                                   $"{info.SegmentSkips} skips, " +
                                   $"{info.SegmentScans.Count} segment scans");
            }
        }

        foreach (var child in RelOpsUnder(relOp))
        {
            WriteRelOp(builder, child, depth + 1, plan, detail);
        }
    }

    private static void WritePlanNode(StringBuilder builder, PlanNode node, int depth, bool detail)
    {
        var indent = new string(' ', depth * 2);

        var target = string.Join(".", new[] { node.Schema, node.Table, node.Index }.Where(p => !string.IsNullOrEmpty(p)));

        builder.AppendLine($"{indent}[{node.NodeId}] {node.PhysicalOperator} ({node.LogicalOperator}) {target} est {node.EstimatedRows} " +
                           $"actual {node.RowsOutput} rows, {node.IoStats?.LogicalReads ?? 0} logical reads, " +
                           $"{node.ExecutionMode}".TrimEnd());

        if (detail && node.PredicateInfo is not null)
        {
            builder.AppendLine($"{indent}    predicate: {string.Concat(node.GetText().Tokens.Select(t => t.ToString()))}");
        }

        foreach (var child in node.Children)
        {
            WritePlanNode(builder, child, depth + 1, detail);
        }
    }

    private static IEnumerable<XElement> RelOpsUnder(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name == ShowPlan + "RelOp")
            {
                yield return child;

                continue;
            }

            foreach (var nested in RelOpsUnder(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<XElement> OwnElements(XElement element, string tag)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name == ShowPlan + "RelOp")
            {
                continue;
            }

            if (child.Name == ShowPlan + tag)
            {
                yield return child;
            }

            foreach (var nested in OwnElements(child, tag))
            {
                yield return nested;
            }
        }
    }

    private static string PlanObject(XElement relOp)
    {
        var element = OwnElements(relOp, "Object").FirstOrDefault();

        if (element is null)
        {
            return string.Empty;
        }

        var parts = new[] { "Schema", "Table", "Index" }.Select(a => Attribute(element, a)).Where(v => !string.IsNullOrEmpty(v));

        return string.Join(".", parts).Replace("[", string.Empty).Replace("]", string.Empty);
    }

    private static string TextOf(XElement element)
    {
        foreach (var scalar in element.Descendants(ShowPlan + "ScalarOperator"))
        {
            if (Attribute(scalar, "ScalarString") is { Length: > 0 } text)
            {
                return text;
            }
        }

        return Format.Truncate(element.ToString(SaveOptions.DisableFormatting), 200);
    }

    private static string Columns(XElement element)
        => string.Join(", ", element.Descendants(ShowPlan + "ColumnReference").Select(c => Attribute(c, "Column") ?? string.Empty));

    private static string Warning(XElement warning)
        => $"{warning.Name.LocalName} {{{string.Join(", ", warning.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}"))}}}";

    private static string? Attribute(XElement element, string name) => (string?)element.Attribute(name);

    private static long Number(XElement element, string name)
        => long.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

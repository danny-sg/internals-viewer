using System.Xml.Linq;
using InternalsViewer.Query.Parsing.Plans.Predicates;

namespace InternalsViewer.Query.Parsing.Plans;

public static class ExecutionPlanParser
{
    public static ExecutionPlan Parse(string xml, PlanHandleRegistry planHandles)
    {
        var doc = XDocument.Parse(xml);

        var queryPlan = doc.Descendants()
                           .FirstOrDefault(e => e.Name.LocalName == "QueryPlan");

        if (queryPlan == null)
        {
            throw new InvalidOperationException("QueryPlan element not found.");
        }

        var planHandleId = planHandles.GetOrAdd(GetPlanHandle(doc));

        var plan = new ExecutionPlan(planHandleId);

        var parameters = PlanParameters.Parse(queryPlan.Parent ?? queryPlan);

        var rootRelationalOperators = queryPlan.Elements()
                                               .Where(e => e.Name.LocalName == "RelOp")
                                               .Select(e => ParseRelationalOperator(e, parameters, 1))
                                               .ToList();

        var statementNode = BuildStatementNode(queryPlan.Parent, rootRelationalOperators);

        plan.Root.Add(statementNode);

        foreach (var root in plan.Root)
        {
            IndexNodes(root, plan.NodesById);
        }

        return plan;
    }

    private static PlanNode BuildStatementNode(XElement? statementElement, List<PlanNode> rootRelOps)
    {
        var statementType = statementElement is null
            ? string.Empty
            : GetStringAttribute(statementElement, "StatementType");

        var subtreeCost = (statementElement is null
                           ? null
                           : GetDoubleAttribute(statementElement, "StatementSubTreeCost"))
                          ?? rootRelOps.Sum(r => r.EstimatedCost ?? 0);

        return new PlanNode
        {
            NodeId = -1,
            IsStatement = true,
            PhysicalOperator = string.IsNullOrEmpty(statementType) ? "Statement" : statementType,
            EstimatedCost = subtreeCost,
            Children = rootRelOps
        };
    }


    public static string GetPlanHandle(XDocument doc)
    {
        var action = doc
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName == "action" &&
                (string?)e.Attribute("name") == "plan_handle");

        return action?.Element("value")?.Value ?? string.Empty;
    }

    private static PlanNode ParseRelationalOperator(XElement element,
                                                    PlanParameters parameters,
                                                    int level = 1)
    {
        var node = new PlanNode
        {
            NodeId = GetIntAttribute(element, "NodeId"),
            PhysicalOperator = GetStringAttribute(element, "PhysicalOp"),
            LogicalOperator = GetStringAttribute(element, "LogicalOp"),
            NodeLevel = level
        };

        node.EstimatedCost = GetDoubleAttribute(element, "EstimatedTotalSubtreeCost");

        node.CountersByThread = ExtractThreadCounters(element);

        ExtractObjectInfo(element, node);

        node.Outputs = ExtractTables(element);

        var children = GetChildRelationalOperators(element);

        if (OperatorClassifier.IsHash(node))
        {
            node.HashInfo = ParseHashInfo(element);
        }

        if (OperatorClassifier.IsDataAccess(node))
        {
            node.ScanInfo = ParseScanInfo(element);

            node.PredicateInfo = ParsePredicateInfo(element, parameters);

            // Showplan XML has no Key Lookup operator - a key lookup arrives as a Clustered Index Seek with Lookup="1" on its IndexScan
            // element. Rename it as SSMS does. (A RID Lookup also carries Lookup="1" but is already named by its PhysicalOp.)
            if (node.ScanInfo.IsLookup &&
                string.Equals(node.PhysicalOperator, "Clustered Index Seek", StringComparison.OrdinalIgnoreCase))
            {
                node.PhysicalOperator = "Key Lookup";
            }
        }

        foreach (var child in children)
        {
            node.Children.Add(ParseRelationalOperator(child, parameters, level + 1));
        }

        return node;
    }

    private static void IndexNodes(PlanNode node, Dictionary<int, PlanNode> dict)
    {
        dict[node.NodeId] = node;

        foreach (var child in node.Children)
        {
            IndexNodes(child, dict);
        }
    }

    private static IEnumerable<XElement> GetChildRelationalOperators(XElement element)
    {
        return element
            .Elements()
            .SelectMany(e =>
                    e.Name.LocalName == "RelOp"
                        ? new[] { e }
                        : e.Elements().Where(c => c.Name.LocalName == "RelOp")
            );
    }

    private static Dictionary<int, ThreadRuntime> ExtractThreadCounters(XElement relOp)
    {
        var counters = new Dictionary<int, ThreadRuntime>();

        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return counters;
        }

        foreach (var counter in runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread"))
        {
            var thread = GetIntAttribute(counter, "Thread");
            var read = GetLongAttribute(counter, "ActualRowsRead") ?? 0;
            var output = GetLongAttribute(counter, "ActualRows") ?? 0;
            var elapsedMs = GetDoubleAttribute(counter, "ActualElapsedms") ?? 0;

            counters[thread] = new ThreadRuntime(read > 0 ? read : output, (long)(elapsedMs * 1000));
        }

        return counters;
    }

    private static int GetIntAttribute(XElement e, string name)
        => (int?)e.Attribute(name) ?? 0;

    private static long? GetLongAttribute(XElement e, string name)
        => (long?)e.Attribute(name);

    private static string GetStringAttribute(XElement e, string name)
        => (string?)e.Attribute(name) ?? string.Empty;

    private static double? GetDoubleAttribute(XElement e, string name)
        => (double?)e.Attribute(name);

    private static void ExtractObjectInfo(XElement element, PlanNode node)
    {
        var objectElement = FindOwnObjectElement(element);

        if (objectElement == null)
        {
            return;
        }

        node.Schema = GetAttribute("Schema", objectElement);
        node.Table = GetAttribute("Table", objectElement);
        node.Index = GetAttribute("Index", objectElement);
    }

    /// <summary>
    /// Finds this RelOp's own Object element (e.g. under its IndexScan/TableScan), without descending into a nested child RelOp. A plain
    /// <c>.Descendants()</c> search would walk into child operators too and pick up the first one's object - so a join/sort/filter with
    /// no object of its own would wrongly inherit its first child's table.
    /// </summary>
    private static XElement? FindOwnObjectElement(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == "RelOp")
            {
                continue;
            }

            if (child.Name.LocalName == "Object")
            {
                return child;
            }

            if (FindOwnObjectElement(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string? GetAttribute(string attributeName, XElement element)
    {
        return ((string?)element.Attribute(attributeName))?.Trim('[', ']');
    }

    private static HashInfo ParseHashInfo(XElement hashElement)
    {
        var info = new HashInfo();

        var build = hashElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "HashKeysBuild");

        if (build != null)
        {
            info.BuildKeys = ParseKeys(build);
        }

        var probe = hashElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "HashKeysProbe");

        if (probe != null)
        {
            info.ProbeKeys = ParseKeys(probe);
        }

        return info;
    }

    /// <summary>
    /// Parses the seek ranges and residual predicate of a data access operator
    /// </summary>
    private static PredicateInfo ParsePredicateInfo(XElement element,
                                                    PlanParameters parameters,
                                                    ColumnOrdinalResolver? resolveOrdinal = null)
    {
        var scanElement = element.Elements()
                                 .FirstOrDefault(e => e.Name.LocalName is "IndexScan" or "TableScan");

        var seekPredicates = scanElement?
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "SeekPredicates");

        var seekParser = new SeekPredicateParser(resolveOrdinal, parameters.Resolve);

        var bounds = seekParser.ParseSeekPredicates(seekPredicates);

        var predicateElement = (scanElement ?? element).Elements()
                                                       .FirstOrDefault(e => e.Name.LocalName == "Predicate");

        var predicateParser = new PredicateParser(resolveOrdinal, parameters.Resolve);

        var residual = predicateParser.ParsePredicateElement(predicateElement);

        return new PredicateInfo
        {
            SeekBounds = bounds,
            Residual = residual,
            HasUntranslatedPredicate = (predicateElement is not null && residual is null) ||
                                       (seekPredicates is not null && bounds.IsDefaultOrEmpty)
        };
    }

    private static ScanInfo ParseScanInfo(XElement scanElement)
    {
        var indexScan = scanElement.Elements().FirstOrDefault(e => e.Name.LocalName == "IndexScan");

        var scanInfo = new ScanInfo();

        if (indexScan != null)
        {
            scanInfo.IsOutputOrdered = (bool?)indexScan.Attribute("Ordered");
            scanInfo.IsLookup = (bool?)indexScan.Attribute("Lookup") ?? false;
        }

        var scanDirection = indexScan?.Attribute("ScanDirection");

        if (scanDirection != null)
        {
            scanInfo.IsForward = scanDirection.Value.Equals("FORWARD", StringComparison.OrdinalIgnoreCase);
        }

        return scanInfo;
    }

    private static List<ColumnReference> ParseKeys(XElement parent)
    {
        return
        [
            .. parent
                .Descendants()
                .Where(e => e.Name.LocalName == "ColumnReference")
                .Select(c => new ColumnReference
                {
                    Database = GetAttribute("Database", c) ?? string.Empty,
                    Schema = GetAttribute("Schema", c) ?? string.Empty,
                    Table = GetAttribute("Table", c) ?? string.Empty,
                    Column = GetAttribute("Column", c) ?? string.Empty
                })
        ];
    }

    public static HashSet<string> ExtractTables(XElement nodeElement)
    {
        return
        [
            .. nodeElement
                .Descendants()
                .Where(e => e.Name.LocalName == "ColumnReference")
                .Select(c => $"{GetAttribute("Schema", c)}.{GetAttribute("Table", c)}")
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t.ToLowerInvariant())
        ];
    }
}

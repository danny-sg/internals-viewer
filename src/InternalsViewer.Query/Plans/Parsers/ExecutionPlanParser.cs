using InternalsViewer.Execution.AccessPaths.Search;
using System.Data;
using System.Xml.Linq;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.AccessPaths.Windowing;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Query.Plans.Parsers.Predicates;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Plans.Parsers;

public static class ExecutionPlanParser
{
    public static ExecutionPlan Parse(string xml, PlanHandleRegistry planHandles)
    {
        var doc = XDocument.Parse(xml);

        var queryPlan = doc.Descendants()
                           .FirstOrDefault(e => e.Name.LocalName == "QueryPlan")
                        ?? throw new InvalidOperationException("QueryPlan element not found.");

        var planHandleId = planHandles.GetOrAdd(GetPlanHandle(doc));

        var plan = new ExecutionPlan(planHandleId);

        var parameters = PlanParameters.Parse(queryPlan.Parent ?? queryPlan);

        var rootRelationalOperators = queryPlan.Elements()
                                               .Where(e => e.Name.LocalName == "RelOp")
                                               .Select(e => ParseRelationalOperator(e, parameters, 1))
                                               .ToList();

        var statementNode = BuildStatementNode(queryPlan.Parent, queryPlan, rootRelationalOperators);

        plan.Root.Add(statementNode);

        foreach (var root in plan.Root)
        {
            IndexNodes(root, plan.NodesById);
        }

        return plan;
    }

    private static PlanNode BuildStatementNode(XElement? statementElement, XElement queryPlan, List<PlanNode> rootRelOps)
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
            QueryMemoryGrant = ParseQueryMemoryGrant(queryPlan),
            Children = rootRelOps
        };
    }

    private static QueryMemoryGrant? ParseQueryMemoryGrant(XElement queryPlan)
    {
        var info = queryPlan.Elements().FirstOrDefault(e => e.Name.LocalName == "MemoryGrantInfo");

        if (info is null)
        {
            return null;
        }

        return new QueryMemoryGrant
        {
            SerialRequiredKb = GetLongAttribute(info, "SerialRequiredMemory"),
            SerialDesiredKb = GetLongAttribute(info, "SerialDesiredMemory"),
            RequiredKb = GetLongAttribute(info, "RequiredMemory"),
            DesiredKb = GetLongAttribute(info, "DesiredMemory"),
            RequestedKb = GetLongAttribute(info, "RequestedMemory"),
            GrantedKb = GetLongAttribute(info, "GrantedMemory"),
            MaxUsedKb = GetLongAttribute(info, "MaxUsedMemory"),
            MaxQueryKb = GetLongAttribute(info, "MaxQueryMemory"),
            GrantWaitTimeSeconds = GetLongAttribute(info, "GrantWaitTime")
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
            NodeLevel = level,
            EstimatedCost = GetDoubleAttribute(element, "EstimatedTotalSubtreeCost"),
            EstimatedRows = (long)Math.Round(GetDoubleAttribute(element, "EstimateRows") ?? 0),
            CountersByThread = ExtractThreadCounters(element),
            IoStats = ParseIoStats(element),
            BatchInfo = ParseBatchInfo(element),
            MemoryGrant = ParseMemoryGrant(element),
            ExecutionMode = GetStringAttribute(element, "EstimatedExecutionMode") == "Row" ? ExecutionMode.Row : ExecutionMode.Batch
        };

        ParseRowCounts(element, node);

        ExtractObjectInfo(element, node);

        node.Outputs = ExtractTables(element);

        node.OutputColumns = ParseOutputColumns(element);
        node.DefinedValues = ParseDefinedValues(element, parameters);
        node.SortColumns = ParseSortColumns(element);
        node.SortInfo = ParseSortInfo(element);
        node.MergeInfo = ParseMergeInfo(element, parameters);
        node.NestedLoopsInfo = ParseNestedLoopsInfo(element, parameters);
        node.GroupByColumns = ParseGroupBy(element);

        var children = GetChildRelationalOperators(element);

        if (OperatorClassifier.IsHash(node))
        {
            node.HashInfo = ParseHashInfo(element, parameters);
        }

        if (OperatorClassifier.IsAggregate(node))
        {
            node.AggregateInfo = ParseAggregateInfo(element, node);
        }

        if (OperatorClassifier.IsSegment(node))
        {
            node.SegmentInfo = ParseSegmentInfo(element, node);
        }

        if (OperatorClassifier.IsSequenceProject(node))
        {
            node.SequenceProjectInfo = ParseSequenceProjectInfo(element);
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

            if (string.Equals(node.Storage, "ColumnStore", StringComparison.OrdinalIgnoreCase)
                && node.PhysicalOperator.Contains("Index Scan", StringComparison.OrdinalIgnoreCase))
            {
                node.PhysicalOperator = "Columnstore Index Scan";
            }
        }
        else if (string.Equals(node.PhysicalOperator, "Filter", StringComparison.OrdinalIgnoreCase))
        {
            node.PredicateInfo = ParsePredicateInfo(element, parameters, _ => -1);
        }

        foreach (var child in children)
        {
            node.Children.Add(ParseRelationalOperator(child, parameters, level + 1));
        }

        if (string.Equals(node.PhysicalOperator, "Top", StringComparison.OrdinalIgnoreCase))
        {
            node.TopInfo = ParseTopInfo(element, parameters);

            if (node.Children is [{ PredicateInfo: { } childPredicateInfo }])
            {
                childPredicateInfo.RowGoal = ParseTopRowCount(element, parameters);
            }
        }

        return node;
    }

    private static TopInfo? ParseTopInfo(XElement element, PlanParameters parameters)
    {
        var top = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Top");

        if (top is null)
        {
            return null;
        }

        return new TopInfo
        {
            RowCount = ParseTopRowCount(element, parameters),
            IsPercent = IsTrue(top.Attribute("IsPercent")),
            WithTies = IsTrue(top.Attribute("WithTies"))
        };
    }

    private static long? ParseTopRowCount(XElement element, PlanParameters parameters)
    {
        var top = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Top");

        if (top is null || IsTrue(top.Attribute("IsPercent")) || IsTrue(top.Attribute("WithTies")))
        {
            return null;
        }

        var scalar = top.Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "TopExpression")?
                        .Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "ScalarOperator");

        var expression = new ScalarOperatorParser(resolveParameter: parameters.Resolve).Parse(scalar);

        if (expression is AccessExpression.Constant { Value.Type: AccessValueType.Integer } constant)
        {
            return constant.Value.Numeric;
        }

        return null;
    }

    private static bool IsTrue(XAttribute? attribute)
    {
        return attribute?.Value is "1" or "true";
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
            var executionMode = GetStringAttribute(counter, "ActualExecutionMode") == "Row" ? ExecutionMode.Row : ExecutionMode.Batch;

            var batches = GetLongAttribute(counter, "Batches") ?? 0;

            counters[thread] = new ThreadRuntime(read > 0 ? read : output, (long)(elapsedMs * 1000), executionMode, batches);
        }

        return counters;
    }

    private static BatchInfo? ParseBatchInfo(XElement relOp)
    {
        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return null;
        }

        var counters = runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread").ToList();

        string[] attributes = ["Batches", "SegmentReads", "SegmentSkips", "ActualLocallyAggregatedRows"];

        if (!counters.Any(c => attributes.Any(a => c.Attribute(a) != null)))
        {
            return null;
        }

        return new BatchInfo
        {
            BatchCount = counters.Sum(c => GetLongAttribute(c, "Batches") ?? 0),
            SegmentReads = counters.Sum(c => GetLongAttribute(c, "SegmentReads") ?? 0),
            SegmentSkips = counters.Sum(c => GetLongAttribute(c, "SegmentSkips") ?? 0),
            LocallyAggregatedRows = counters.Sum(c => GetLongAttribute(c, "ActualLocallyAggregatedRows") ?? 0)
        };
    }

    private static void ParseRowCounts(XElement relOp, PlanNode node)
    {
        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return;
        }

        var counters = runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread").ToList();

        node.RowsOutput = counters.Sum(c => GetLongAttribute(c, "ActualRows") ?? 0);

        node.RowsRead = counters.Any(c => c.Attribute("ActualRowsRead") is not null)
            ? counters.Sum(c => GetLongAttribute(c, "ActualRowsRead") ?? 0)
            : null;
    }

    private static PlanIoStatistics? ParseIoStats(XElement relOp)
    {
        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return null;
        }

        var counters = runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread").ToList();

        if (!counters.Any(c => c.Attribute("ActualLogicalReads") is not null))
        {
            return null;
        }

        return new PlanIoStatistics
        {
            LogicalReads = counters.Sum(c => GetLongAttribute(c, "ActualLogicalReads") ?? 0),
            PhysicalReads = counters.Sum(c => GetLongAttribute(c, "ActualPhysicalReads") ?? 0),
            ReadAheads = counters.Sum(c => GetLongAttribute(c, "ActualReadAheads") ?? 0),
            Scans = counters.Sum(c => GetLongAttribute(c, "ActualScans") ?? 0),
            Rebinds = counters.Sum(c => GetLongAttribute(c, "ActualRebinds") ?? 0),
            Rewinds = counters.Sum(c => GetLongAttribute(c, "ActualRewinds") ?? 0),
            LobLogicalReads = counters.Sum(c => GetLongAttribute(c, "ActualLobLogicalReads") ?? 0),
            LobPhysicalReads = counters.Sum(c => GetLongAttribute(c, "ActualLobPhysicalReads") ?? 0),
            LobReadAheads = counters.Sum(c => GetLongAttribute(c, "ActualLobReadAheads") ?? 0)
        };
    }

    private static PlanMemoryGrant? ParseMemoryGrant(XElement relOp)
    {
        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return null;
        }

        var counters = runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread").ToList();

        var input = SumAcrossThreads(counters, "InputMemoryGrant");
        var output = SumAcrossThreads(counters, "OutputMemoryGrant");
        var used = SumAcrossThreads(counters, "UsedMemoryGrant");

        if (input is null && output is null && used is null)
        {
            return null;
        }

        return new PlanMemoryGrant
        {
            InputKb = input,
            OutputKb = output,
            UsedKb = used
        };
    }

    private static long? SumAcrossThreads(List<XElement> counters, string attributeName)
    {
        return counters.Any(c => c.Attribute(attributeName) is not null)
            ? counters.Sum(c => GetLongAttribute(c, attributeName) ?? 0)
            : null;
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
        node.Storage = GetAttribute("Storage", objectElement);
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

    /// <summary>
    /// Parses the hash keys and probe residual of a hash operator
    /// </summary>
    /// <remarks>
    /// Scoped to the operator's own Hash element rather than searching its descendants, because a hash operator's inputs are nested inside
    /// that element and carry hash elements of their own.
    /// </remarks>
    private static HashInfo ParseHashInfo(XElement relOp, PlanParameters parameters)
    {
        var info = new HashInfo();

        var hash = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "Hash");

        if (hash is null)
        {
            return info;
        }

        var build = hash.Elements().FirstOrDefault(e => e.Name.LocalName == "HashKeysBuild");

        if (build != null)
        {
            info.BuildKeys = ParseKeys(build);
        }

        var probe = hash.Elements().FirstOrDefault(e => e.Name.LocalName == "HashKeysProbe");

        if (probe != null)
        {
            info.ProbeKeys = ParseKeys(probe);
        }

        var residualElement = hash.Elements().FirstOrDefault(e => e.Name.LocalName == "ProbeResidual");

        info.Residual = new PredicateParser(null, parameters.Resolve).ParsePredicateElement(residualElement);

        info.HasUntranslatedResidual = residualElement is not null && info.Residual is null;

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
                                 .FirstOrDefault(e => e.Name.LocalName is "IndexScan" or "TableScan" or "Filter");

        var seekPredicates = scanElement?.Elements()
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
            CorrelatedSeekColumns = [.. seekParser.CorrelatedColumns],
            Residual = residual,
            HasUntranslatedPredicate = (predicateElement is not null && residual is null) ||
                                       (seekPredicates is not null && bounds.IsDefaultOrEmpty && seekParser.CorrelatedColumns.Count == 0)
        };
    }

    private static ScanInfo ParseScanInfo(XElement scanElement)
    {
        var indexScan = scanElement.Elements().FirstOrDefault(e => e.Name.LocalName is "IndexScan" or "TableScan");

        var scanInfo = new ScanInfo();

        if (indexScan != null)
        {
            scanInfo.IsOutputOrdered = (bool?)indexScan.Attribute("Ordered");
            scanInfo.IsLookup = (bool?)indexScan.Attribute("Lookup") ?? false;
            scanInfo.IsForcedIndex = (bool?)indexScan.Attribute("ForcedIndex") ?? false;
            scanInfo.IsForceSeek = (bool?)indexScan.Attribute("ForceSeek") ?? false;
            scanInfo.IsForceScan = (bool?)indexScan.Attribute("ForceScan") ?? false;
        }

        var scanDirection = indexScan?.Attribute("ScanDirection");

        if (scanDirection != null)
        {
            scanInfo.IsForward = scanDirection.Value.Equals("FORWARD", StringComparison.OrdinalIgnoreCase);
        }

        return scanInfo;
    }

    private static List<ColumnReference> ParseOutputColumns(XElement element)
    {
        var outputList = element.Elements().FirstOrDefault(e => e.Name.LocalName == "OutputList");

        return outputList is null ? [] : ParseKeys(outputList);
    }

    private static List<DefinedValueInfo> ParseDefinedValues(XElement element, PlanParameters parameters)
    {
        var definedValues = FindDefinedValues(element);

        if (definedValues is null)
        {
            return [];
        }

        var expressionParser = new PredicateParser(_ => -1, parameters.Resolve);

        var result = new List<DefinedValueInfo>();

        foreach (var definedValue in definedValues.Elements().Where(e => e.Name.LocalName == "DefinedValue"))
        {
            var columns = new List<ColumnReference>();

            foreach (var child in definedValue.Elements())
            {
                if (child.Name.LocalName == "ColumnReference")
                {
                    columns.Add(ReadColumn(child));
                }
                else if (child.Name.LocalName == "ValueVector")
                {
                    columns.AddRange(child.Elements()
                                          .Where(e => e.Name.LocalName == "ColumnReference")
                                          .Select(ReadColumn));
                }
            }

            if (columns.Count == 0)
            {
                continue;
            }

            var scalar = definedValue.Elements().FirstOrDefault(e => e.Name.LocalName == "ScalarOperator");

            result.Add(new DefinedValueInfo
            {
                Columns = columns,
                Expression = scalar?.Attribute("ScalarString")?.Value,
                ParsedExpression = expressionParser.ParseExpression(scalar),
                DataType = ParseConvertDataType(scalar)
            });
        }

        return result;
    }

    private static SqlDbType? ParseConvertDataType(XElement? scalar)
    {
        var convert = scalar?.Elements().FirstOrDefault();

        return convert?.Name.LocalName == "Convert" ? ShowplanDataType.Parse(convert.Attribute("DataType")?.Value) : null;
    }

    private static AggregateInfo ParseAggregateInfo(XElement element, PlanNode node)
    {
        var columns = new List<AggregateColumn>();

        foreach (var definedValue in node.DefinedValues)
        {
            if (definedValue.Columns.Count != 1
                || definedValue.ParsedExpression is not AccessExpression.Aggregate aggregate
                || AggregateFunctions.Parse(aggregate.Name) is not { } function)
            {
                continue;
            }

            columns.Add(new AggregateColumn(definedValue.Columns[0].Column.Trim('[', ']'), function)
            {
                Argument = aggregate.Arguments.Length > 0 ? aggregate.Arguments[0] : null,
                IsDistinct = aggregate.IsDistinct
            });
        }

        return new AggregateInfo
        {
            GroupBy = GroupingColumns(node),
            Columns = columns,
            HasUntranslatedAggregate = columns.Count < CountAggregates(element)
        };
    }

    private static SegmentInfo ParseSegmentInfo(XElement element, PlanNode node)
    {
        var column = element.Elements()
                            .FirstOrDefault(e => e.Name.LocalName == "Segment")?
                            .Elements()
                            .FirstOrDefault(e => e.Name.LocalName == "SegmentColumn")?
                            .Elements()
                            .FirstOrDefault(e => e.Name.LocalName == "ColumnReference");

        return new SegmentInfo
        {
            GroupBy = node.GroupByColumns,
            SegmentColumn = column is null ? null : ReadColumn(column)
        };
    }

    /// <summary>
    /// Reads the ranking functions a Sequence Project defines
    /// </summary>
    /// <remarks>
    /// A ranking function is a running count rather than an expression over one row, so it is read straight from the Sequence element
    /// instead of going through the scalar parser, which has nothing it could evaluate it to. Any defined value that does not resolve to
    /// a known function leaves the operator untranslated rather than being dropped, which would return the wrong row.
    /// </remarks>
    private static SequenceProjectInfo ParseSequenceProjectInfo(XElement element)
    {
        var definedValues = FindDefinedValues(element)?
                            .Elements()
                            .Where(e => e.Name.LocalName == "DefinedValue")
                            .ToList() ?? [];

        var columns = new List<RankingColumn>();

        foreach (var definedValue in definedValues)
        {
            var column = definedValue.Elements().FirstOrDefault(e => e.Name.LocalName == "ColumnReference");

            var sequence = definedValue.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sequence");

            if (column is null
                || sequence is null
                || RankingFunctions.Parse(sequence.Attribute("FunctionName")?.Value) is not { } function)
            {
                continue;
            }

            columns.Add(new RankingColumn(ReadColumn(column).Column.Trim('[', ']'), function));
        }

        return new SequenceProjectInfo
        {
            Columns = columns,
            HasUntranslatedFunction = columns.Count < definedValues.Count
        };
    }

    private static List<ColumnReference> GroupingColumns(PlanNode node)
    {
        if (node.GroupByColumns.Count > 0)
        {
            return node.GroupByColumns;
        }

        return OperatorClassifier.IsHash(node) ? node.HashInfo?.BuildKeys ?? [] : [];
    }

    private static int CountAggregates(XElement element)
        => FindDefinedValues(element)?.Descendants().Count(e => e.Name.LocalName == "Aggregate") ?? 0;

    private static XElement? FindDefinedValues(XElement element)
        => element.Elements().FirstOrDefault(e => e.Name.LocalName == "DefinedValues")
           ?? element.Elements()
                     .SelectMany(e => e.Elements())
                     .FirstOrDefault(e => e.Name.LocalName == "DefinedValues");

    private static SortInfo? ParseSortInfo(XElement element)
    {
        var sort = element.Elements().FirstOrDefault(e => e.Name.LocalName is "Sort" or "TopSort");

        if (sort is null)
        {
            return null;
        }

        long? rows = null;

        if (sort.Name.LocalName == "TopSort" && long.TryParse(sort.Attribute("Rows")?.Value, out var parsed))
        {
            rows = parsed;
        }

        return new SortInfo(IsTrue(sort.Attribute("Distinct")), rows, IsTrue(sort.Attribute("WithTies")));
    }

    private static List<SortColumnInfo> ParseSortColumns(XElement element)
    {
        var orderBy = element.Elements()
                             .FirstOrDefault(e => e.Name.LocalName is "Sort" or "TopSort")?
                             .Elements()
                             .FirstOrDefault(e => e.Name.LocalName == "OrderBy");

        if (orderBy is null)
        {
            return [];
        }

        var result = new List<SortColumnInfo>();

        foreach (var orderByColumn in orderBy.Elements().Where(e => e.Name.LocalName == "OrderByColumn"))
        {
            var columns = ParseKeys(orderByColumn);

            if (columns.Count == 0)
            {
                continue;
            }

            var ascending = orderByColumn.Attribute("Ascending") is not { } attribute || IsTrue(attribute);

            result.Add(new SortColumnInfo(columns[0], ascending));
        }

        return result;
    }

    private static MergeInfo? ParseMergeInfo(XElement element, PlanParameters parameters)
    {
        var merge = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Merge");

        if (merge is null)
        {
            return null;
        }

        var outer = merge.Elements().FirstOrDefault(e => e.Name.LocalName == "OuterSideJoinColumns");

        var inner = merge.Elements().FirstOrDefault(e => e.Name.LocalName == "InnerSideJoinColumns");

        var residualElement = merge.Elements().FirstOrDefault(e => e.Name.LocalName == "Residual");

        if (outer is null && inner is null && residualElement is null)
        {
            return null;
        }

        var residual = new PredicateParser(null, parameters.Resolve).ParsePredicateElement(residualElement);

        return new MergeInfo
        {
            OuterKeys = outer is null ? [] : ParseKeys(outer),
            InnerKeys = inner is null ? [] : ParseKeys(inner),
            ManyToMany = (bool?)merge.Attribute("ManyToMany") ?? false,
            Residual = residual,
            HasUntranslatedResidual = residualElement is not null && residual is null
        };
    }

    /// <summary>
    /// Parses the predicate a loop join applies to the rows its inner side returned
    /// </summary>
    private static NestedLoopsInfo? ParseNestedLoopsInfo(XElement element, PlanParameters parameters)
    {
        var loops = element.Elements().FirstOrDefault(e => e.Name.LocalName == "NestedLoops");

        var predicateElement = loops?.Elements().FirstOrDefault(e => e.Name.LocalName == "Predicate");

        if (predicateElement is null)
        {
            return null;
        }

        var predicate = new PredicateParser(null, parameters.Resolve).ParsePredicateElement(predicateElement);

        return new NestedLoopsInfo
        {
            Predicate = predicate,
            HasUntranslatedPredicate = predicate is null
        };
    }

    private static List<ColumnReference> ParseGroupBy(XElement element)
    {
        var groupBy = element.Elements()
                             .SelectMany(e => e.Elements())
                             .FirstOrDefault(e => e.Name.LocalName == "GroupBy");

        return groupBy is null ? [] : ParseKeys(groupBy);
    }

    private static List<ColumnReference> ParseKeys(XElement parent)
    {
        return
        [
            .. parent
                .Descendants()
                .Where(e => e.Name.LocalName == "ColumnReference")
                .Select(ReadColumn)
        ];
    }

    private static ColumnReference ReadColumn(XElement element)
    {
        return new ColumnReference
        {
            Database = GetAttribute("Database", element) ?? string.Empty,
            Schema = GetAttribute("Schema", element) ?? string.Empty,
            Table = GetAttribute("Table", element) ?? string.Empty,
            Column = GetAttribute("Column", element) ?? string.Empty
        };
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

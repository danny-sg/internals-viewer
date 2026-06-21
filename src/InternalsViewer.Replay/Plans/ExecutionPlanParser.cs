using System.Xml.Linq;

namespace InternalsViewer.Replay.Plans;

public static class ExecutionPlanParser
{
    public static ExecutionPlan Parse(string xml)
    {
        var doc = XDocument.Parse(xml);

        var queryPlan = doc
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "QueryPlan");

        if (queryPlan == null)
        {
            throw new InvalidOperationException("QueryPlan element not found.");
        }

        var planHandle = GetPlanHandle(doc);

        var plan = new ExecutionPlan(planHandle);

        var rootRelOps = queryPlan
            .Elements()
            .Where(e => e.Name.LocalName == "RelOp");

        foreach (var relOp in rootRelOps)
        {
            var node = ParseRelationalOperator(relOp);
            plan.Roots.Add(node);
        }

        foreach (var root in plan.Roots)
        {
            IndexNodes(root, plan.NodesById);
        }

        return plan;
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

    private static PlanNode ParseRelationalOperator(XElement element)
    {
        var node = new PlanNode
        {
            NodeId = GetIntAttribute(element, "NodeId"),
            PhysicalOperator = GetStringAttribute(element, "PhysicalOp"),
            LogicalOperator = GetStringAttribute(element, "LogicalOp")
        };

        node.EstimatedCost = GetDoubleAttribute(element, "EstimatedTotalSubtreeCost");

        ExtractObjectInfo(element, node);

        var children = GetChildRelOps(element);

        foreach (var child in children)
        {
            node.Children.Add(ParseRelationalOperator(child));
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


    private static IEnumerable<XElement> GetChildRelOps(XElement element)
    {
        // Look through known container nodes (Hash, NestedLoops, etc.)
        return element
            .Elements()
            .SelectMany(e =>
                    e.Name.LocalName == "RelOp"
                        ? new[] { e }                           // direct child
                        : e.Elements().Where(c => c.Name.LocalName == "RelOp") // nested under operator container
            );
    }


    private static int GetIntAttribute(XElement e, string name)
        => (int?)e.Attribute(name) ?? 0;

    private static string GetStringAttribute(XElement e, string name)
        => (string)e.Attribute(name) ?? string.Empty;

    private static double? GetDoubleAttribute(XElement e, string name)
        => (double?)e.Attribute(name);

    private static void ExtractObjectInfo(XElement element, PlanNode node)
    {
        var obj = element
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Object");

        if (obj == null)
        {
            return;
        }

        node.Schema = ((string?)obj.Attribute("Schema"))?.Trim('[', ']');
        node.Table = ((string?)obj.Attribute("Table"))?.Trim('[', ']');
        node.Index = ((string?)obj.Attribute("Index"))?.Trim('[', ']');
    }
}
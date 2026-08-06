using System;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed record OperatorHeader(string Physical, string Heading, string Subheading, Uri? Icon)
{
    public static OperatorHeader For(IteratorDefinition definition, PlanNode? node)
    {
        var physical = node?.PhysicalOperator is { Length: > 0 } name ? name : TraceLayoutBuilder.DisplayName(definition);

        var heading = definition.NodeId < 0 ? physical : $"{physical} ({definition.NodeId})";

        var logical = node?.LogicalOperator ?? string.Empty;

        var subheading = logical.Length > 0 && logical != physical ? logical : string.Empty;

        return new OperatorHeader(physical, heading, subheading, node is null ? null : PlanIconResolver.Resolve(node));
    }
}

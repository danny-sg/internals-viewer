using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Replay.Plans;


public sealed class PlanNode
{
    public int NodeId { get; set; }

    public string PhysicalOperator { get; set; } = string.Empty;

    public string LogicalOperator { get; set; } = string.Empty;

    public List<PlanNode> Children { get; set; } = new();

    // runtime attachment (your engine)
    public OperatorExecution Execution { get; set; }

    public string? Schema { get; set; }

    public string? Table { get; set; }

    public string? Index { get; set; }

    public double? EstimatedCost { get; set; }
}
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.Models.Query.Trace.Lineage;

public sealed class LineageStage(string operatorName, string type, PlanOutputStage stage, LineageStage? consumer)
{
    public string Operator { get; } = operatorName;

    public string Type { get; } = type;

    public PlanOutputStage Stage { get; } = stage;

    public LineageStage? Consumer { get; } = consumer;

    public double X { get; set; }

    public double Y { get; set; }

    public string Value { get; set; } = string.Empty;
}

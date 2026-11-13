using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathStage(string operatorName, string type, PlanOutputStage? stage, DataPathStage? consumer)
{
    public string Operator { get; } = operatorName;

    public string Type { get; } = type;

    public PlanOutputStage? Stage { get; } = stage;

    public DataPathStage? Consumer { get; } = consumer;

    public bool IsBuild { get; init; }

    public double X { get; set; }

    public double Y { get; set; }

    public string Value { get; set; } = string.Empty;
}

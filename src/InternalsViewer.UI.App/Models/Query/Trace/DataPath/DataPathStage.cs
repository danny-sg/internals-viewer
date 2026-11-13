using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathStage(string name, string kind, string source, ulong sourceAddress, DataPathStage? consumer)
{
    public string Name { get; } = name;

    public string Kind { get; } = kind;

    public string Source { get; } = source;

    public ulong SourceAddress { get; } = sourceAddress;

    public DataPathStage? Consumer { get; } = consumer;

    public string Detail { get; init; } = string.Empty;

    public bool IsCalculated { get; init; }

    public PlanOutputStage? Stage { get; init; }

    public bool IsBuild { get; init; }

    public (TracePosition From, TracePosition To)? Scope { get; init; }

    public string Value { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsInScope => Value.Length > 0 && IsActive;

    public double X { get; set; }

    public double Y { get; set; }
}

using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputStage(ulong reference, int depth, int consumer)
{
    public ulong Reference { get; } = reference;

    public int Depth { get; } = depth;

    public int Consumer { get; } = consumer;

    public ulong PlanObject { get; internal set; }

    public ulong Iterator { get; internal set; }

    public PlanOutputStageType Type { get; internal set; }

    public ulong Address { get; internal set; }

    public SqlTypeInfo? ColumnType { get; internal set; }

    internal PlanOutputValueSource? Value { get; set; }

    internal PlanOutputValueSource? Record { get; set; }
}

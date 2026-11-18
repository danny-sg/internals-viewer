using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public readonly record struct PlanOutputLink(PlanOutputLinkType Type, ulong Address, TracePosition Position)
{
    public int Slot { get; init; } = -1;

    public int? Offset { get; init; }

    public ulong Source { get; init; }

    public int SourceOffset { get; init; }

    public int Depth { get; init; }

    public MemoryField? Field { get; init; }

    public bool IsRoute { get; init; }

    public SqlTypeInfo? ColumnType { get; init; }

    public string? ClassName { get; init; }
}

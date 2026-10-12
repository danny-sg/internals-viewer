using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Export;

internal sealed class ExportReferences(Func<long, AllocationUnit?> allocationUnitOf)
{
    public ReferenceTable<CallStackNode> Nodes { get; } = new();

    public ReferenceTable<CallstackFrame> Frames { get; } = new();

    public ReferenceTable<EngineEvent> Events { get; } = new();

    public ReferenceTable<LogRecord> LogRecords { get; } = new();

    public HashSet<EngineEvent> TopLevel { get; } = new(ReferenceEqualityComparer.Instance);

    public List<EngineEvent> TopLevelOrder { get; } = [];

    public Func<long, AllocationUnit?> AllocationUnitOf { get; } = allocationUnitOf;
}

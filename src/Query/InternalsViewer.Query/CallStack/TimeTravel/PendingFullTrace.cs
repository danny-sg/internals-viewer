using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.Debugging.TimeTravel;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record PendingFullTrace(TimeTravelTrace Trace,
                                      IReadOnlyList<EngineEvent> Events,
                                      string SymbolsPath,
                                      MemoryClerkSnapshot MemoryClerks,
                                      IReadOnlyList<RawEvent> RawEvents)
{
    public IReadOnlySet<uint> ThreadIds { get; } = RawEvents.Where(e => e.SystemThreadId is not null)
                                                            .Select(e => e.SystemThreadId!.Value)
                                                            .ToHashSet();
}

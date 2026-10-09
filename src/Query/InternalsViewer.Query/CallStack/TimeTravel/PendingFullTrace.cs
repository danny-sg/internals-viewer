using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.Debugging.TimeTravel;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record PendingFullTrace(TimeTravelTrace Trace,
                                      IReadOnlyList<EngineEvent> Events,
                                      IReadOnlySet<uint> ThreadIds,
                                      string SymbolsPath,
                                      MemoryClerkSnapshot MemoryClerks,
                                      IReadOnlyList<RawEvent> RawEvents);

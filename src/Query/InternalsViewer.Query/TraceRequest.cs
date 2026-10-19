using InternalsViewer.Query.Events;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query;

/// <summary>
/// Pre-parsed Full Trace
/// </summary>
/// <remarks>
/// Pre-parsed trace containing information for parsing, including the trace file, engine events, symbols path, memory clerk snapshot,
/// and raw events.
///
/// Scoped threads are derived from the raw events where the event has SystemThreadId - this is important for parallel workers.
/// </remarks>
public sealed record TraceRequest(TimeTravelTraceFile TraceFile,
                                  IReadOnlyList<EngineEvent> Events,
                                  string SymbolsPath,
                                  MemoryClerkSnapshot MemoryClerks,
                                  ObjectPoolSnapshot ObjectPool,
                                  IReadOnlySet<uint> ThreadIds);

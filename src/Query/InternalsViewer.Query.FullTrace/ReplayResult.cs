using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Raw replay results
/// </summary>
/// <param name="Nodes">
/// Function call reference nodes including parent index, absolute address, instance, and call count
/// </param>
/// <param name="Modules">
/// Modules loaded during the trace recording
/// </param>
/// <param name="CallLog">
/// Function calls including sequence, thread, registers, return value, and raw node index
/// </param>
/// <param name="Timeline">
/// Hierarchical/row layout start and end position for each call per thread
/// </param>
internal sealed record ReplayResult(CallNode[] Nodes,
                                    IReadOnlyList<TraceModule> Modules,
                                    FullTraceCallLog CallLog,
                                    FullTraceTimeline Timeline,
                                    EventMarker[] Markers);

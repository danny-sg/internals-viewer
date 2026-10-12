using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel;

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
internal sealed record TimeTravelReplay(TimeTravelCallNode[] Nodes,
                                        IReadOnlyList<TimeTravelModule> Modules,
                                        TimeTravelCallLog CallLog,
                                        TimeTravelTimeline Timeline);

using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelReplay(TimeTravelCallNode[] Nodes,
                                      IReadOnlyList<TimeTravelModule> Modules,
                                      TimeTravelCallLog CallLog,
                                      TimeTravelTimeline Timeline);

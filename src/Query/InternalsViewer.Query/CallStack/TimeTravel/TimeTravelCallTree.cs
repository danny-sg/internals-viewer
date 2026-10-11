using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelCallTree(TimeTravelCallNode[] Nodes, TimeTravelModule[] Modules);

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelModule(string Path, ulong Address, ulong Size);

public sealed record TimeTravelCallTree(TimeTravelCallNode[] Nodes,
                                        TimeTravelCallActivity[] Activity,
                                        TimeTravelModule[] Modules,
                                        TimeTravelCallLog? CallLog = null);

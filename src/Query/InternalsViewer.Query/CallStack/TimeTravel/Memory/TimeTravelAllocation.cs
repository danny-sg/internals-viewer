namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

internal readonly record struct TimeTravelAllocation(uint Thread,
                                                   double Start,
                                                   double End,
                                                   ulong Bytes,
                                                   ulong Pointer,
                                                   bool Workspace = false);

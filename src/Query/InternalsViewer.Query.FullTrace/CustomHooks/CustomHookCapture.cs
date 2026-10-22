namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed record CustomHookCapture(string Name,
                                       ulong FunctionAddress,
                                       ulong InstanceAddress,
                                       int CallIndex,
                                       TracePosition Position,
                                       uint Thread,
                                       ulong Address,
                                       byte[] Data);

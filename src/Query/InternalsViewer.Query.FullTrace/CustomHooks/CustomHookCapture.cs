namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed record CustomHookCapture(string Name,
                                       ulong Function,
                                       ulong Instance,
                                       int Call,
                                       TracePosition Position,
                                       uint Thread,
                                       ulong Address,
                                       byte[] Data);

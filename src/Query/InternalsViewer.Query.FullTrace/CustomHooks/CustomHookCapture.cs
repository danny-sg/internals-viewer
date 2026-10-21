namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed record CustomHookCapture(string Name,
                                       ulong Function,
                                       ulong Instance,
                                       int CallIndex,
                                       TracePosition Position,
                                       uint Thread,
                                       ulong Address,
                                       byte[] Data);

namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed record CustomHookCapture(string Name,
                                       ulong Function,
                                       ulong Instance,
                                       int Call,
                                       ulong Id,
                                       uint Thread,
                                       ulong Address,
                                       byte[] Data);

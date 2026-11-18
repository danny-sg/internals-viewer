namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;

internal readonly record struct StackObject(ulong Address,
                                            int Size,
                                            uint Thread,
                                            int OwnerReplayNodeIndex,
                                            TracePosition FromPosition,
                                            TracePosition ToPosition);

namespace InternalsViewer.Query.FullTrace.Memory.History.StackObjects;

internal readonly record struct StackObject(ulong Address,
                                            int Size,
                                            uint Thread,
                                            int OwnerCallNodeIndex,
                                            TracePosition FirstPosition,
                                            TracePosition EndPosition);

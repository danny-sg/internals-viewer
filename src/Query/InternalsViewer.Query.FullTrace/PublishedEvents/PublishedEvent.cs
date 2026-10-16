namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal readonly record struct PublishedEvent(ulong Publisher,
                                               ulong SessionBuffer,
                                               TracePosition Position,
                                               ulong Instructions,
                                               int Node,
                                               uint Thread);

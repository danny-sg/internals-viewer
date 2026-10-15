namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal readonly record struct PublishedEvent(ulong Publisher,
                                               ulong SessionBuffer,
                                               ulong Sequence,
                                               ulong Steps,
                                               ulong Instructions,
                                               int Node,
                                               uint Thread);

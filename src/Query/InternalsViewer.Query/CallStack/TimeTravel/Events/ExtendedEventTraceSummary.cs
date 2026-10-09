namespace InternalsViewer.Query.CallStack.TimeTravel.Events;

public sealed record ExtendedEventTraceSummary(long Published,
                                               long Written,
                                               long InFile,
                                               ulong SessionBuffer,
                                               IReadOnlyList<(string Name, long Written, long InFile)> Differences,
                                               IReadOnlyList<(ulong Worker, uint Thread)> Workers,
                                               int Tasks);

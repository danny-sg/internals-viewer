namespace InternalsViewer.Query.Events;

public sealed record RawEvent(string Name, uint? SystemThreadId, ulong? WorkerAddress, ulong? TaskAddress);

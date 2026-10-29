namespace InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

public enum BufferLatchState
{
    Released,
    Shared,
    Exclusive,
    Waiting
}

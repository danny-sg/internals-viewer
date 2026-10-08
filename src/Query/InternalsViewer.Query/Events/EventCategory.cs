namespace InternalsViewer.Query.Events;

/// <summary>Coarse buckets used to group lock and wait events for display.</summary>
public enum EventCategory
{
    Io = 0,
    Cpu = 1,
    Concurrency = 2,
    Parallelism = 3,
}
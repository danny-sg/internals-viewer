using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Locks;

namespace InternalsViewer.Replay.Events;

/// <summary>Coarse buckets used to group lock and wait events for display.</summary>
public enum EventCategory
{
    Io = 0,
    Cpu = 1,
    Concurrency = 2,
    Parallelism = 3,
}

/// <summary>
/// Buckets lock and wait events into one of four families (IO, CPU, Concurrency, Parallelism),
/// classifying waits by wait-type name in the same spirit as the SQL Server wait categories.
/// </summary>
public static class EventCategoryClassifier
{
    public const int CategoryCount = 4;

    public static EventCategory? GetCategory(EngineEvent engineEvent) => engineEvent switch
    {
        // A lock is always a concurrency/blocking concern.
        LockEvent => EventCategory.Concurrency,
        WaitEvent wait => Categorise(wait.WaitType),
        _ => null,
    };

    public static EventCategory Categorise(WaitType waitType)
    {
        var name = waitType.ToString();

        if (IsParallelism(name)) return EventCategory.Parallelism;
        if (IsIo(name)) return EventCategory.Io;
        if (IsCpu(name)) return EventCategory.Cpu;
        if (IsConcurrency(name)) return EventCategory.Concurrency;

        // Memory grants, compilation, preemptive, etc. fold into CPU as the catch-all.
        return EventCategory.Cpu;
    }

    private static bool IsParallelism(string name) =>
        name.StartsWith("CX") ||              // CXPACKET, CXCONSUMER, CXSYNC_*
        name == "EXCHANGE" ||
        name == "EXECSYNC" ||
        name.StartsWith("HASH_TABLE") ||
        name.StartsWith("BITMAP");

    private static bool IsIo(string name) =>
        name.StartsWith("PAGEIOLATCH") ||     // buffer IO
        name == "WRITELOG" || name == "LOGBUFFER" || name.StartsWith("LOGMGR") || // transaction log IO
        name.Contains("IO_COMPLETION") ||
        name == "ASYNC_IO_COMPLETION" ||
        name == "NETWORK_IO" ||
        name == "BACKUPIO" ||
        name.StartsWith("DISKIO") ||
        name == "WRITE_COMPLETION" ||
        name == "IO_RETRY" ||
        name == "IO_QUEUE_LIMIT";

    private static bool IsCpu(string name) =>
        name == "SOS_SCHEDULER_YIELD" ||
        name == "THREADPOOL" ||
        name.StartsWith("SOS_WORKER") ||
        name == "CMEMTHREAD" ||
        name == "CMEMPARTITIONED";

    private static bool IsConcurrency(string name) =>
        name.StartsWith("LCK_") ||            // locks
        name.StartsWith("LATCH") ||           // non-buffer latches
        name.StartsWith("PAGELATCH") ||       // buffer (non-IO) latches
        name.StartsWith("TRANMARKLATCH") ||
        name.Contains("TRANSACTION");
}

using InternalsViewer.Replay.Events.EventTypes;

namespace InternalsViewer.Replay.Plans;

public static class NodeEventHelper
{
    public static long? GetFirstActivityTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is QueryThreadEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Min();
    }

    public static long? GetFirstIoTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is IoEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Min();
    }

    public static long? GetLastActivityTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is QueryThreadEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Max();
    }

    public static long? GetLastIoTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is IoEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Max();
    }

    public static long? GetFirstLogTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is TransactionLogEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Min();
    }

    public static long? GetLastLogTime(List<EngineEvent> events, PlanNodeIdentifier identifier)
    {
        return events.Where(e => e.PlanNodeIdentifier == identifier && e is TransactionLogEvent)
                     .Select(e => (long?)e.TimeMs)
                     .Max();
    }
}
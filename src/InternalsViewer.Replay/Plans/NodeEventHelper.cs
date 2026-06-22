using InternalsViewer.Replay.Events.EventTypes;

namespace InternalsViewer.Replay.Plans;

public static class NodeEventHelper
{
    // Projecting to long? makes Min/Max return null for an empty set, so callers can distinguish
    // "no matching event" from a genuine time of 0.
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


}
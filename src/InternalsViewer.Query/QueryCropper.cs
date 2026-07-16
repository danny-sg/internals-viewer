using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Memory;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query;

internal class QueryCropper
{
    private const long CropPaddingUs = 100;

    private const long AdditionalPaddingUs = 500;

    public static  (long? start, long? end) GetCropTiming(List<EngineEvent> events)
    {
        long? startTimeUs = null;
        long? endTImeUs = null;

        if (CropWindow(events) is var (start, end, planHandle))
        {
            bool Overlaps(EngineEvent e) => e.TimeUs <= end && e.TimeUs + e.DurationUs >= start;

            events = events.Where(e => Overlaps(e)
                                       || (e is not LockEvent
                                           && planHandle != PlanHandleRegistry.None
                                           && e.PlanHandleId == planHandle))
                .ToList();

            static long EndUs(EngineEvent e) =>
                e is QueryThreadEvent or MemoryEvent ? e.TimeUs : e.TimeUs + e.DurationUs;

            var windowEvents = events.Where(Overlaps).ToList();

            startTimeUs = (windowEvents.Count > 0 ? Math.Min(start, windowEvents.Min(e => e.TimeUs)) : start)
                           - AdditionalPaddingUs;

            endTImeUs = (windowEvents.Count > 0 ? Math.Max(end, windowEvents.Max(EndUs)) : end)
                         + AdditionalPaddingUs;
        }

        return (startTimeUs, endTImeUs);
    }

    private static (long Start, long End, short PlanHandle)? CropWindow(List<EngineEvent> events)
    {
        var queryNode = events.FirstOrDefault(e => e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 });

        return queryNode?.PlanNodeIdentifier is not { } id
            ? null
            : (
                Math.Max(0, queryNode.TimeUs - CropPaddingUs),
                queryNode.TimeUs + queryNode.DurationUs + CropPaddingUs,
                id.PlanHandleId
            );
    }
}

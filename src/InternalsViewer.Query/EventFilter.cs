using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query;

public static class EventFilter
{
    public static List<EngineEvent> Filter(List<EngineEvent> events, EventOptions eventOptions)
    {
        return FilterLockCategories(events, eventOptions.IncludeLockModeCategories);
    }

    private static List<EngineEvent> FilterLockCategories(List<EngineEvent> events, HashSet<LockModeCategory> categories)
    {
        return events.Where(Keep).ToList();

        bool Keep(EngineEvent e) => e switch
        {
            LockEvent l => categories.Contains(LockModeClassifier.Categorise(l.LockMode)),

            LockGroup g => g.Events
                            .OfType<LockEvent>()
                            .Any(l => categories.Contains(LockModeClassifier.Categorise(l.LockMode))),

            LockEscalationEvent esc => categories.Contains(LockModeClassifier.Categorise(esc.LockMode)),

            _ => true,
        };
    }
}
namespace InternalsViewer.Query.Events.Reads;

internal static class ReadAheadClassifier
{
    public static void Classify(IReadOnlyList<EngineEvent> events)
    {
        foreach (var engineEvent in events)
        {
            if (engineEvent is ReadEventGroup read)
            {
                read.IsReadAhead = read.PageCount > 1;
            }
        }
    }
}

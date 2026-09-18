using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Events.Reads;

public static class ReadAheadClassifier
{
    public static void Classify(IReadOnlyList<EngineEvent> events)
    {
        var known = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        foreach (var engineEvent in events)
        {
            if (engineEvent is not ReadEventGroup read)
            {
                continue;
            }

            read.IsReadAhead = IsReadAhead(read.CallStack, known) || read.Events.Any(e => IsReadAhead(e.CallStack, known));
        }
    }

    private static bool IsReadAhead(CallStackNode? leaf, Dictionary<CallStackNode, bool> known)
    {
        if (leaf is null)
        {
            return false;
        }

        if (known.TryGetValue(leaf, out var cached))
        {
            return cached;
        }

        var result = leaf.Path().Any(f => f.Resolved is { } frame
                                          && (frame.MethodName.Contains("ReadAhead", StringComparison.Ordinal)
                                              || frame.ClassName?.Contains("ReadAhead", StringComparison.Ordinal) == true));

        known[leaf] = result;

        return result;
    }
}

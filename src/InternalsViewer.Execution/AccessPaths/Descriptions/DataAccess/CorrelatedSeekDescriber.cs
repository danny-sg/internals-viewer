using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.DataAccess;

/// <summary>
/// Describes a seek re-run for each outer row, which is a key lookup or the inner side of a loop join
/// </summary>
public static class CorrelatedSeekDescriber
{
    public static OperatorDescription Describe(AccessStrategy? strategy)
    {
        var bind = new AccessStrategyPhase
        {
            Phase = AccessPhase.Rebind,
            Title = "Rebind",
            Lead = "Seek values are set from the outer row. Every rebind resets the seek to the new correlated seek bounds."
        };

        return new OperatorDescription
        {
            Summary = "Access path re-run once per outer row, descending the index again for the key values bound from that row.",
            IsStreaming = true,
            Phases = [bind, .. strategy?.Phases ?? []]
        };
    }
}

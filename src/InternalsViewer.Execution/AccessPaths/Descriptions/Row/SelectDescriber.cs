using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions.Row;

public static class SelectDescriber
{
    public static OperatorDescription Describe()
    {
        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Open,
            Title = "Open",
            Lead = "Opening cascades down the tree, each operator opening its inputs before any row is read. A blocking operator does " +
                   "its work on the first row asked for, not here"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Fetch,
            Title = "Fetch",
            Lead = "Each request pulls a single row up through the tree. The tree is demand driven, so nothing below runs further than " +
                   "the row asked for needs"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "Closing cascades down. A client that stops reading early closes the tree mid-walk, which is why a cancelled query " +
                   "can leave a scan part read"
        });

        return new OperatorDescription
        {
            Summary = "The root of the plan, which pulls one row at a time from the operator below and hands it to the client. Every " +
                      "operator in the tree runs because this one asked for a row",
            IsStreaming = true,
            Phases = phases.ToImmutable()
        };
    }
}

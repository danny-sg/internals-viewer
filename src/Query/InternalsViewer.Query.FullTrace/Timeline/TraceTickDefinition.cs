using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial record TraceTickDefinition(string Name,
                                                 string Module,
                                                 string Function,
                                                 string Colour,
                                                 double HeightFactor,
                                                 double WidthFactor,
                                                 IReadOnlyList<TraceCallCondition> Conditions)
{
    public bool IsFunction(CallStackNode node)
        => node.Frame is { } frame && string.Equals(frame.Module, Module, StringComparison.OrdinalIgnoreCase) && node.Symbol == Function;

    public bool MatchesCall(TraceArgumentCall call) => TraceCallCondition.MatchesAll(Conditions, call);

    public bool MatchesNode(CallStackNode node) => Conditions.All(c => c.MatchesNode(node));

    public bool MatchesOperator(ExecutionOperatorEvent? operatorEvent) => Conditions.All(c => c.MatchesOperator(operatorEvent));
}

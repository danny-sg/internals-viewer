using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed partial record TraceTickDefinition(string Name,
                                                 string Module,
                                                 string Function,
                                                 string Colour,
                                                 double HeightFactor,
                                                 double WidthFactor,
                                                 IReadOnlyList<TraceCallCondition> Conditions)
{
    public const string CategoryColour = "Category";

    private const string AnyModule = "*";

    private const char FunctionSeparator = '|';

    public string? Label { get; init; }

    public TraceTickSourceType Source { get; init; }

    public byte Value { get; init; }

    private GlobPattern[] Functions
        => field ??= [.. Function.Split(FunctionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                 .Select(GlobPattern.Parse)];

    public string GetLabel() => Label ?? Function;

    public bool IsFunction(CallTreeNode node)
        => node.Frame is { } frame
           && (Module == AnyModule || string.Equals(frame.Module.Name, Module, StringComparison.OrdinalIgnoreCase))
           && Functions.Any(f => f.Matches(node.Symbol));

    public bool MatchesCall(TraceArgumentCall call) => TraceCallCondition.MatchesAll(Conditions, call);

    public bool MatchesNode(CallTreeNode node) => Conditions.All(c => c.MatchesNode(node));

    public bool MatchesOperator(ExecutionOperatorEvent? operatorEvent) => Conditions.All(c => c.MatchesOperator(operatorEvent));
}

using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceEvaluationReader
{
    private const int ResultRegister = 2;

    private const string ExpressionServiceClass = "CEsExec";

    private static HashSet<string> EvaluationMethods { get; } = new(StringComparer.Ordinal)
    {
        "FastCompare",
        "FastCompare1",
        "GeneralEval",
        "GeneralEval4"
    };

    internal static TraceTickSeries Build(TraceTimeline timeline,
                                          TraceCallStore calls,
                                          TraceOperatorLifetimeList lifetimes,
                                          EnclosingOperatorCallIndex? enclosing = null)
        => TraceTickSeries.FromCalls(timeline,
                                     calls,
                                     lifetimes,
                                     IsEvaluation,
                                     (_, c) => c.GetLocator(ResultRegister) is { ValueType: LocatorValueType.Variant } locator
                                               && GetResult(new VariantLocator(locator)) is { } result
                                                   ? (byte)result
                                                   : null,
                                     enclosing ?? EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));

    private static bool IsEvaluation(CallFrame frame)
        => frame.Function is { ClassName: ExpressionServiceClass, MethodName: { } method }
           && EvaluationMethods.Contains(method);

    internal static TraceEvaluationResult? GetResult(VariantLocator variant) => variant switch
    {
        { Status: VariantStatus.Null } => TraceEvaluationResult.Unknown,
        _ when variant.TryGetBoolean(out var value) => value ? TraceEvaluationResult.True : TraceEvaluationResult.False,
        _ => null
    };
}

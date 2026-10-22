using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceEvaluations
{
    private const string ExpressionService = "CEsExec";

    private const int ResultRegister = 2;

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "FastCompare",
        "FastCompare1",
        "GeneralEval",
        "GeneralEval4"
    };

    internal static TraceTickSeries Build(TraceTimeline timeline,
                                          TraceCallStore calls,
                                          TraceOperatorLifetimes lifetimes,
                                          EnclosingOperatorCalls? enclosing = null)
        => TraceTickSeries.FromCalls(timeline,
                                     calls,
                                     lifetimes,
                                     IsEvaluation,
                                     (_, c) => c.GetLocator(ResultRegister) is { ValueType: LocatorValueType.Variant } locator
                                               && GetResult(new VariantLocator(locator)) is { } result
                                                   ? (byte)result
                                                   : null,
                                     enclosing ?? EnclosingOperatorCalls.Build(calls, lifetimes.OperatorsByInstance));

    private static bool IsEvaluation(CallstackFrame frame)
        => frame.Resolved is { ClassName: ExpressionService, MethodName: { } method } && Methods.Contains(method);

    internal static TraceEvaluationResult? GetResult(VariantLocator variant) => variant switch
    {
        { Status: VariantStatus.Null } => TraceEvaluationResult.Unknown,
        _ when variant.TryGetBoolean(out var value) => value ? TraceEvaluationResult.True : TraceEvaluationResult.False,
        _ => null
    };
}

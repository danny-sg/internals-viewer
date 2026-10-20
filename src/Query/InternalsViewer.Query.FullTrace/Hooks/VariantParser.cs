using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal static class VariantParser
{
    public static TraceEvaluationResult? GetPredicateResult(ITraceReplay replay, ulong variant)
        => replay.TryRead(variant + VariantLayout.StatusOffset, out byte status)
           && replay.TryRead(variant + VariantLayout.ValueOffset, out byte value)
            ? GetResult(status, value)
            : null;

    private static TraceEvaluationResult? GetResult(byte status, byte value) => (status, value) switch
    {
        (VariantLayout.NullStatus, _) => TraceEvaluationResult.Unknown,
        (VariantLayout.ValueStatus, 0) => TraceEvaluationResult.False,
        (VariantLayout.ValueStatus, 1) => TraceEvaluationResult.True,
        _ => null
    };
}

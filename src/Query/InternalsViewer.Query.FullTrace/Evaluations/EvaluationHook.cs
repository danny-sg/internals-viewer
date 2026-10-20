using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Evaluations;

internal sealed class EvaluationHook(EvaluationFunctions functions) : ITraceHook
{
    private const int ResultArgument = 2;

    public IEnumerable<TraceHookFunction> Functions
        => [.. functions.Functions.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return))];

    private Dictionary<ulong, ulong> Pending { get; } = [];

    private List<EvaluationCapture> Captures { get; } = [];

    public EvaluationCapture[] Build() => [.. Captures];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (call.Arguments[ResultArgument] >= TraceAddress.Smallest)
        {
            Pending[call.Id] = call.Arguments[ResultArgument];
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Pending.Remove(call.Id, out var result) || VariantParser.GetPredicateResult(replay, result) is not { } evaluation)
        {
            return;
        }

        Captures.Add(new EvaluationCapture(call.Function, (int)call.Call, evaluation));
    }

    public void OnAbandoned(in TraceHookCall call) => Pending.Remove(call.Id);
}

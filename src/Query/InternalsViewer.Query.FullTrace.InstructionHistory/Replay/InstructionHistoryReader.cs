using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public sealed class InstructionHistoryReader : IInstructionHistoryReader
{
    public const string TraceUnavailableMessage =
        "The Full Trace recording for this run is no longer available. "
        + "A recording is replaced when the next Full Trace starts, and run exports do not include it.";

    private const long MaximumSteps = 1_000_000;

    public async Task<TraceInstructionHistory> ReadAsync(TimeTravelTraceFile? trace,
                                                         InstructionRequest request,
                                                         CancellationToken cancellationToken)
    {
        if (trace is null || !File.Exists(trace.Path))
        {
            throw new InvalidOperationException(TraceUnavailableMessage);
        }

        using var session = await ReplaySession.OpenAsync(trace, cancellationToken);

        var hook = new InstructionStepHook();

        var outcome = await session.ReplayAsync(request.Thread,
                                                request.From,
                                                request.To,
                                                request.Frame.Address,
                                                MaximumSteps,
                                                [hook],
                                                null,
                                                cancellationToken);

        return hook.Builder.Build((TraceInstructionOutcome)outcome);
    }
}

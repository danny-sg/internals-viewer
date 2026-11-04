using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public interface IInstructionHistoryReader
{
    Task<TraceInstructionHistory> ReadAsync(TimeTravelTraceFile? trace,
                                            InstructionRequest request,
                                            CancellationToken cancellationToken);
}

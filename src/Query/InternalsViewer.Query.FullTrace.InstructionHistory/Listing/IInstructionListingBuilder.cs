using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public interface IInstructionListingBuilder
{
    Task<InstructionListing> BuildAsync(InstructionRequest request,
                                        TraceInstructionHistory history,
                                        CancellationToken cancellationToken);
}

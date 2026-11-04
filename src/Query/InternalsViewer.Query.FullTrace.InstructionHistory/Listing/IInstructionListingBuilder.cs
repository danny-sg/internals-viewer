namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public interface IInstructionListingBuilder
{
    Task<InstructionListing> BuildAsync(InstructionRequest request, CancellationToken cancellationToken);
}

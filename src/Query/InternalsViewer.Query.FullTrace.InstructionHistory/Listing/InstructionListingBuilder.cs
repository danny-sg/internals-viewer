namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public sealed class InstructionListingBuilder : IInstructionListingBuilder
{
    public Task<InstructionListing> BuildAsync(InstructionRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

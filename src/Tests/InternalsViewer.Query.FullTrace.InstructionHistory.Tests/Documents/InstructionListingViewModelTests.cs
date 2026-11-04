using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.InstructionHistory.Documents;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Documents;

[Trait("Category", "Unit")]
public class InstructionListingViewModelTests
{
    private static readonly InstructionRequest Request = new(new CallstackFrame(), 1, new TracePosition(10, 0), new TracePosition(20, 0));

    [Fact]
    public async Task Loading_Shows_The_Lines_The_Builder_Returns()
    {
        var lines = new List<InstructionLine> { new(0x1000, "First", null), new(0x1004, "Second", "Explanation") };

        var model = Create(_ => Task.FromResult(new InstructionListing(lines)));

        await model.LoadAsync();

        Assert.Same(lines, model.Lines);
        Assert.Null(model.Message);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task A_Builder_Failure_Is_Shown_As_The_Message()
    {
        var model = Create(_ => throw new InvalidOperationException("Not Available"));

        await model.LoadAsync();

        Assert.Equal("Not Available", model.Message);
        Assert.Empty(model.Lines);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task Disposing_Cancels_A_Listing_Still_Being_Built()
    {
        var model = Create(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);

            return new InstructionListing([]);
        });

        var loading = model.LoadAsync();

        model.Dispose();

        await loading;

        Assert.Null(model.Message);
        Assert.False(model.IsLoading);
    }

    private static InstructionListingViewModel Create(Func<CancellationToken, Task<InstructionListing>> build)
        => new(new FakeListingBuilder(build), Request, "Function Instructions");

    private sealed class FakeListingBuilder(Func<CancellationToken, Task<InstructionListing>> build) : IInstructionListingBuilder
    {
        public Task<InstructionListing> BuildAsync(InstructionRequest request, CancellationToken cancellationToken)
            => build(cancellationToken);
    }
}

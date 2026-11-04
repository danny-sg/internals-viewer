using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.InstructionHistory.Documents;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Documents;

[Trait("Category", "Unit")]
public class InstructionListingViewModelTests
{
    private static readonly InstructionRequest Request = new(new CallstackFrame(), 1, new TracePosition(10, 0), new TracePosition(20, 0));

    private static readonly Func<CancellationToken, Task<TraceInstructionHistory>> Replayed
        = _ => Task.FromResult(TraceInstructionHistory.Empty);

    [Fact]
    public async Task Loading_Shows_The_Lines_The_Builder_Returns()
    {
        var lines = new List<InstructionLine> { new(0x1000, "First", null), new(0x1004, "Second", "Explanation") };

        var model = Create(Replayed, _ => Task.FromResult(new InstructionListing(lines)));

        await model.LoadAsync();

        Assert.Same(lines, model.Lines);
        Assert.Null(model.Message);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task The_Summary_Counts_The_Replayed_Instructions_And_Addresses()
    {
        var model = Create(Replayed, _ => Task.FromResult(new InstructionListing([])));

        await model.LoadAsync();

        Assert.Equal("0 Instructions, 0 Addresses", model.Summary);
        Assert.Same(TraceInstructionHistory.Empty, model.History);
    }

    [Fact]
    public async Task A_Replay_Failure_Is_Shown_As_The_Message()
    {
        var model = Create(_ => throw new InvalidOperationException(InstructionHistoryReader.TraceUnavailableMessage),
                           _ => Task.FromResult(new InstructionListing([])));

        await model.LoadAsync();

        Assert.Equal(InstructionHistoryReader.TraceUnavailableMessage, model.Message);
        Assert.Null(model.Summary);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task A_Builder_Failure_Is_Shown_As_The_Message()
    {
        var model = Create(Replayed, _ => throw new InvalidOperationException("Not Available"));

        await model.LoadAsync();

        Assert.Equal("Not Available", model.Message);
        Assert.Empty(model.Lines);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task Disposing_Cancels_A_Replay_Still_Running()
    {
        var model = Create(async token =>
                           {
                               await Task.Delay(Timeout.Infinite, token);

                               return TraceInstructionHistory.Empty;
                           },
                           _ => Task.FromResult(new InstructionListing([])));

        var loading = model.LoadAsync();

        model.Dispose();

        await loading;

        Assert.Null(model.Message);
        Assert.False(model.IsLoading);
    }

    private static InstructionListingViewModel Create(Func<CancellationToken, Task<TraceInstructionHistory>> read,
                                                      Func<CancellationToken, Task<InstructionListing>> build)
        => new(new FakeHistoryReader(read), new FakeListingBuilder(build), null, Request, "Function Instructions");

    private sealed class FakeHistoryReader(Func<CancellationToken, Task<TraceInstructionHistory>> read) : IInstructionHistoryReader
    {
        public Task<TraceInstructionHistory> ReadAsync(TimeTravelTraceFile? trace,
                                                       InstructionRequest request,
                                                       CancellationToken cancellationToken)
            => read(cancellationToken);
    }

    private sealed class FakeListingBuilder(Func<CancellationToken, Task<InstructionListing>> build) : IInstructionListingBuilder
    {
        public Task<InstructionListing> BuildAsync(InstructionRequest request,
                                                   TraceInstructionHistory history,
                                                   CancellationToken cancellationToken)
            => build(cancellationToken);
    }
}

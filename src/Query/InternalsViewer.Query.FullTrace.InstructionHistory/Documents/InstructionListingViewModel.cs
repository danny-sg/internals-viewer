using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Documents;

public sealed partial class InstructionListingViewModel(IInstructionHistoryReader reader,
                                                        IInstructionListingBuilder builder,
                                                        TimeTravelTraceFile? trace,
                                                        InstructionRequest request,
                                                        string title)
    : ObservableObject, IDisposable
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _summary;

    [ObservableProperty]
    private TraceInstructionHistory _history = TraceInstructionHistory.Empty;

    [ObservableProperty]
    private List<InstructionLine> _lines = [];

    public string Title { get; } = title;

    public InstructionRequest Request { get; } = request;

    private CancellationTokenSource Cancellation { get; } = new();

    public async Task LoadAsync()
    {
        IsLoading = true;

        Message = null;

        try
        {
            History = await reader.ReadAsync(trace, Request, Cancellation.Token);

            Summary = GetSummary(History);

            var listing = await builder.BuildAsync(Request, History, Cancellation.Token);

            Lines = listing.Lines;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        Cancellation.Cancel();

        Cancellation.Dispose();
    }

    private static string GetSummary(TraceInstructionHistory history)
    {
        var summary = $"{history.Steps.Count:N0} Instructions, {history.AddressCount:N0} Addresses";

        return history.Outcome == TraceInstructionOutcome.StepLimitReached ? summary + ", Step Limit Reached" : summary;
    }
}

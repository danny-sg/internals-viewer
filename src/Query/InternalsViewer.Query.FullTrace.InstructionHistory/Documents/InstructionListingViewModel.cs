using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Documents;

public sealed partial class InstructionListingViewModel(IInstructionListingBuilder builder, InstructionRequest request, string title)
    : ObservableObject, IDisposable
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _message;

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
            var listing = await builder.BuildAsync(Request, Cancellation.Token);

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
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Services.Mcp;

public sealed class McpDocumentSource : IMcpDocumentSource
{
    private ConditionalWeakTable<QueryViewModel, string> Ids { get; } = [];

    private int Count { get; set; }

    public Task<List<IMcpDocument>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        if (App.MainWindow is not { } window)
        {
            return Task.FromResult(new List<IMcpDocument>());
        }

        var completion = new TaskCompletionSource<List<IMcpDocument>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(Snapshot(window));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return queued ? completion.Task.WaitAsync(cancellationToken) : Task.FromResult(new List<IMcpDocument>());
    }

    private List<IMcpDocument> Snapshot(MainWindow window)
        => [.. window.GetQueryViewModels().Select(q => new QueryMcpDocument(GetId(q.ViewModel), q.ViewModel, q.IsActive, window.DispatcherQueue))];

    private string GetId(QueryViewModel viewModel) => Ids.GetValue(viewModel, _ => $"q{++Count}");
}

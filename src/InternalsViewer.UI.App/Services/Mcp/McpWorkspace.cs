using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.UI.App.Messages;

namespace InternalsViewer.UI.App.Services.Mcp;

public sealed class McpWorkspace : IMcpWorkspace
{
    public Task OpenPageAsync(DatabaseSource database,
                              PageAddress pageAddress,
                              ushort? slot,
                              CancellationToken cancellationToken)
        => SendAsync(() => new OpenPageMessage(new OpenPageRequest(database, pageAddress) { Slot = slot }), cancellationToken);

    public Task OpenIndexAsync(DatabaseSource database, PageAddress rootPage, CancellationToken cancellationToken)
        => SendAsync(() => new OpenIndexMessage(new OpenIndexRequest(database, rootPage)), cancellationToken);

    public Task OpenColumnstoreAsync(DatabaseSource database, long allocationUnitId, CancellationToken cancellationToken)
        => SendAsync(() => new OpenColumnstoreMessage(new OpenColumnstoreRequest(database, allocationUnitId)), cancellationToken);

    private static async Task SendAsync<TMessage>(Func<TMessage> create, CancellationToken cancellationToken)
        where TMessage : AsyncRequestMessage<bool>
    {
        var window = App.MainWindow ?? throw new InvalidOperationException("The Internals Viewer window is not available.");

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.SetResult(await WeakReferenceMessenger.Default.Send(create()));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        if (!queued)
        {
            throw new InvalidOperationException("The Internals Viewer window is not available.");
        }

        if (!await completion.Task.WaitAsync(cancellationToken))
        {
            throw new InvalidOperationException("The tab could not be opened.");
        }
    }
}

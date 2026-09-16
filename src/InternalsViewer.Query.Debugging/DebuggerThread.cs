using System.Collections.Concurrent;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// One thread that runs every call made to a debugger engine client in the order they arrive
/// </summary>
internal sealed class DebuggerThread : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();

    public DebuggerThread()
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = "Debugger Engine Client" };

        thread.Start();
    }

    public Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _work.Add(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);

                    return;
                }

                try
                {
                    completion.SetResult(action());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            completion.SetException(new ObjectDisposedException(nameof(DebuggerThread)));
        }

        return completion.Task;
    }

    public void Dispose() => _work.CompleteAdding();

    private void Loop()
    {
        foreach (var item in _work.GetConsumingEnumerable())
        {
            item();
        }
    }
}

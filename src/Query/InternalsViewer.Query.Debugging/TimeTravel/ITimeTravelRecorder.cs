namespace InternalsViewer.Query.Debugging.TimeTravel;

public interface ITimeTravelRecorder
{
    Task<ITimeTravelRecording> PrepareAsync(string connectionString, string sessionId, CancellationToken cancellationToken);
}

public interface ITimeTravelRecording : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<TimeTravelTrace> StopAsync(CancellationToken cancellationToken);
}

namespace InternalsViewer.Query.Debugging.TimeTravel;

public interface ITimeTravelRecorder
{
    Task<ITimeTravelRecording> PrepareAsync(string connectionString, CancellationToken cancellationToken);
}

public interface ITimeTravelRecording : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<TimeTravelTrace> StopAsync(CancellationToken cancellationToken);
}

namespace InternalsViewer.Query.Recording;

public interface ITimeTravelRecorder
{
    IReadOnlyList<string> Modules { get; }

    bool IsLicenceAccepted { get; }

    Task<ITimeTravelRecording> PrepareAsync(string connectionString, string sessionId, CancellationToken cancellationToken);

    void PrepareRecorder();

    string ReadLicence();

    Task AcceptLicenceAsync(CancellationToken cancellationToken);
}

public interface ITimeTravelRecording : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<TimeTravelTraceFile> StopAsync(CancellationToken cancellationToken);
}

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.Server;
using InternalsViewer.Query.FullTrace.Interfaces.Recording;

namespace InternalsViewer.Query.FullTrace.Recording;

[SupportedOSPlatform("windows")]
public sealed class TimeTravelRecorder(IWinDbgSettings settings, ILogger<TimeTravelRecorder> logger) : ITimeTravelRecorder
{
    private const string OnlineSql = "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Status') AS nvarchar(60))";

    private static readonly TimeSpan RestartTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public static string TraceRoot => Path.Combine(Root, "Traces");

    public IReadOnlyList<string> Modules => TimeTravelRecording.Modules;

    public bool IsLicenceAccepted => TimeTravelLicence.IsAccepted;

    private static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "InternalsViewer",
                     "TimeTravel");

    private static string RecorderRoot => Path.Combine(Root, "Recorder");

    public async Task<ITimeTravelRecording> PrepareAsync(string connectionString, string sessionId, CancellationToken cancellationToken)
    {
        var process = await SqlServerProcess.GetAsync(connectionString, cancellationToken);

        if (!process.IsLocal)
        {
            throw new DebuggerException($"SQL Server is running on {process.MachineName}. TTD can only record a process "
                                      + $"on this machine ({Environment.MachineName})");
        }

        var directory = PrepareTimeTravel();

        if (!TimeTravelLicence.IsAccepted)
        {
            throw new DebuggerException("The TTD licence has not been accepted. Turn Record Full Trace off and on again to "
                                      + "review and accept it");
        }

        var recording = new TimeTravelRecording(directory, CreateTraceDirectory(sessionId, process), process.ProcessId, sessionId, logger);

        try
        {
            await recording.PrepareAsync(cancellationToken);

            if (recording.Restarted)
            {
                await WaitForDatabaseAsync(connectionString, cancellationToken);
            }
        }
        catch
        {
            await recording.DisposeAsync();

            throw;
        }

        return recording;
    }

    public void PrepareRecorder() => PrepareTimeTravel();

    public string ReadLicence()
    {
        var directory = PrepareTimeTravel();

        return TimeTravelLicence.Read(directory)
               ?? throw new DebuggerException("The TTD licence terms could not be read. Run "
                                            + $"\"{Path.Combine(directory, "TTD.exe")}\" once from an elevated prompt to accept it");
    }

    public Task AcceptLicenceAsync(CancellationToken cancellationToken)
        => TimeTravelLicence.AcceptAsync(PrepareTimeTravel(), cancellationToken);

    private async Task WaitForDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        SqlConnection.ClearAllPools();

        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);

                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(OnlineSql, connection);

                if (await command.ExecuteScalarAsync(cancellationToken) is "ONLINE")
                {
                    return;
                }
            }
            catch (SqlException exception) when (Stopwatch.GetElapsedTime(started) < RestartTimeout)
            {
                logger.LogDebug("SQL Server not available after restart: {Message}", exception.Message);
            }

            if (Stopwatch.GetElapsedTime(started) > RestartTimeout)
            {
                throw new DebuggerException($"SQL Server did not come back online within {RestartTimeout.TotalMinutes} minutes of "
                                          + "restarting for the recording");
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    private string PrepareTimeTravel()
    {
        var installation = WinDbgInstallation.Resolve(settings.WinDbgPath, logger);

        var source = Path.Combine(installation.EngineDirectory, "ttd");

        logger.LogInformation("Looking for Time Travel Debugging in {Directory}", source);

        try
        {
            var directory = installation.PrepareTimeTravel(RecorderRoot);

            if (directory is null)
            {
                logger.LogWarning("TTD.exe was not found in {Directory}", source);

                throw new WinDbgNotFoundException($"Time Travel Debugging was not found in {installation.EngineDirectory}");
            }

            logger.LogInformation("Time Travel Debugging recorder: {Directory}", directory);

            return directory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DebuggerException($"TTD could not be copied from {installation.EngineDirectory}: {exception.Message}");
        }
    }

    private string CreateTraceDirectory(string sessionId, SqlServerProcess process)
    {
        var root = Directory.CreateDirectory(TraceRoot);

        foreach (var previous in root.GetDirectories())
        {
            try
            {
                previous.Delete(recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug("Previous trace {Directory} could not be deleted: {Message}", previous.FullName, exception.Message);
            }
        }

        var service = ServiceAccess.ServiceSid(process.ServiceName);

        if (service is null)
        {
            logger.LogWarning("No service account found for {Service}. Every service can write the trace folder", process.ServiceName);
        }

        return ServiceAccess.CreatePrivate(Path.Combine(root.FullName, sessionId), service);
    }
}

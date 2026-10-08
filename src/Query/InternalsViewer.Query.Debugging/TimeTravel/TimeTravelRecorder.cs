using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using InternalsViewer.Query.Debugging.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace InternalsViewer.Query.Debugging.TimeTravel;

[SupportedOSPlatform("windows")]
public sealed class TimeTravelRecorder(WinDbgService winDbg, ILogger<TimeTravelRecorder> logger) : ITimeTravelRecorder
{
    private const string LicenceKey = @"Software\Microsoft\TTD";

    private const string LicenceValue = "EULASigned";

    private const string OnlineSql = "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Status') AS nvarchar(60))";

    private static readonly TimeSpan RestartTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public static string TraceRoot => Path.Combine(Root, "Traces");

    private static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "InternalsViewer",
                     "TimeTravel");

    private static string RecorderRoot => Path.Combine(Root, "Recorder");

    public async Task<ITimeTravelRecording> PrepareAsync(string connectionString, CancellationToken cancellationToken)
    {
        var process = await SqlServerProcess.GetAsync(connectionString, cancellationToken);

        if (!process.IsLocal)
        {
            throw new DebuggerException($"SQL Server is running on {process.MachineName}. TTD can only record a process "
                                      + $"on this machine ({Environment.MachineName})");
        }

        var directory = PrepareTimeTravel();

        if (!IsLicenceAccepted())
        {
            throw new DebuggerException("The TTD licence has not been accepted. Run "
                                      + $"\"{Path.Combine(directory, "TTD.exe")}\" once from an elevated prompt to accept it");
        }

        var recording = new TimeTravelRecording(directory, CreateTraceDirectory(), process.ProcessId, logger);

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
        var installation = winDbg.ResolveInstallation();

        try
        {
            return installation.PrepareTimeTravel(RecorderRoot)
                   ?? throw new DebuggerException($"Time Travel Debugging was not found in {installation.EngineDirectory}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DebuggerException($"TTD could not be copied from {installation.EngineDirectory}: {exception.Message}");
        }
    }

    private static bool IsLicenceAccepted()
    {
        using var key = Registry.CurrentUser.OpenSubKey(LicenceKey);

        return key?.GetValue(LicenceValue) is int and not 0;
    }

    private string CreateTraceDirectory()
    {
        var root = Directory.CreateDirectory(TraceRoot);

        ServiceAccess.Grant(root.FullName, FileSystemRights.Modify);

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

        return Directory.CreateDirectory(Path.Combine(root.FullName, DateTime.Now.ToString("yyyyMMdd-HHmmss"))).FullName;
    }
}

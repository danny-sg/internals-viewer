using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.Debugging;
using InternalsViewer.Query.Debugging.Exceptions;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.Services.Query.Debugging;

/// <summary>
/// The app's one connection to a WinDbg session, and the means of starting one attached to SQL Server
/// </summary>
/// <remarks>
/// WinDbg has to run elevated to attach to SQL Server, so starting it goes through a UAC prompt. It is started
/// listening on a named pipe, and once it is up the app joins it as a second client through a
/// <see cref="DebuggerHost"/> process, which is this executable started again with the host switch. A session the
/// user started themselves works the same way after they type the <c>.server</c> command into it. Nothing about the
/// debugger is touched until one of these is used, so a machine without WinDbg loses only this.
/// </remarks>
public sealed class WinDbgService(SettingsViewModel settings, ILogger<WinDbgService> logger) : IDisposable
{
    private const string PipeName = "InternalsViewer";

    private const string ClearBreakpointsCommand = "bc *";

    private const string DetachCommand = ".detach";

    private const int ElevationDeclined = 1223;

    private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(10);

    private DebuggerHostClient? _host;

    public event EventHandler? StatusChanged;

    public string Status { get; private set; } = "Not Connected";

    public bool IsConnected => _host is not null;

    public WinDbgServerOptions ServerOptions =>
        new(PipeName, string.IsNullOrWhiteSpace(settings.WinDbgPassword) ? null : settings.WinDbgPassword);

    private static string EngineCacheRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InternalsViewer", "WinDbg");

    /// <summary>
    /// Starts WinDbg elevated, attached to the SQL Server behind the connection string, and joins its session
    /// </summary>
    public async Task AttachAsync(string connectionString, CancellationToken cancellationToken)
    {
        var process = await SqlServerProcess.GetAsync(connectionString, cancellationToken);

        if (!process.IsLocal)
        {
            throw new DebuggerException($"SQL Server is running on {process.MachineName}. WinDbg can only attach to a "
                                      + $"process on this machine ({Environment.MachineName})");
        }

        var installation = ResolveInstallation();

        var engine = PrepareEngine(installation);

        SetStatus($"Starting WinDbg attached to process {process.ProcessId}");

        Launch(installation.Executable, $"{ServerOptions.LaunchSwitch} -p {process.ProcessId} -c g");

        await ConnectAsync(engine, AttachTimeout, cancellationToken);
    }

    /// <summary>
    /// Joins a WinDbg session already listening on the pipe
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken) =>
        ConnectAsync(PrepareEngine(ResolveInstallation()), ConnectTimeout, cancellationToken);

    /// <summary>
    /// Runs a command in the session, joining one first if the app is not yet connected
    /// </summary>
    public async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        if (_host is null)
        {
            await ConnectAsync(cancellationToken);
        }

        try
        {
            await _host!.SendAsync(command, cancellationToken);

            logger.LogInformation("Sent to WinDbg: {Command}", command);

            SetStatus($"Sent {command}");
        }
        catch (DebuggerException)
        {
            Disconnect();

            throw;
        }
    }

    /// <summary>
    /// Clears every breakpoint, detaches the debugger from SQL Server, and drops the connection
    /// </summary>
    /// <remarks>
    /// Best effort: a session that cannot be reached is dropped regardless. The resume the session issues after a
    /// command fails once the target has been detached, so that error is expected and ignored.
    /// </remarks>
    public async Task DetachAsync()
    {
        if (_host is { } host)
        {
            try
            {
                await host.SendAsync($"{ClearBreakpointsCommand}; {DetachCommand}", CancellationToken.None);
            }
            catch (DebuggerException exception)
            {
                logger.LogDebug("Detaching from the target: {Message}", exception.Message);
            }
        }

        Disconnect();
    }

    public void Disconnect()
    {
        _host?.Dispose();

        _host = null;

        SetStatus("Not Connected");
    }

    public void Dispose()
    {
        if (_host is not null && !DetachAsync().Wait(DisposeTimeout))
        {
            Disconnect();
        }
    }

    private async Task ConnectAsync(string engine, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Disconnect();

        var options = ServerOptions;

        var started = Stopwatch.GetTimestamp();

        SetStatus("Connecting to WinDbg");

        var host = DebuggerHostClient.Start(Environment.ProcessPath!, DebuggerHost.Switch);

        while (true)
        {
            try
            {
                await host.ConnectAsync(engine, options.RemoteOptions, cancellationToken);

                _host = host;

                SetStatus("Connected to WinDbg");

                return;
            }
            catch (DebuggerException exception) when (!exception.IsEngineFailure && Stopwatch.GetElapsedTime(started) < timeout)
            {
                logger.LogDebug("WinDbg not reachable yet: {Message}", exception.Message);

                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (DebuggerException exception)
            {
                host.Dispose();

                SetStatus("Not Connected");

                throw exception.IsEngineFailure
                    ? exception
                    : new DebuggerException($"{exception.Message}. Use Attach WinDbg to SQL Server, or type "
                                          + $"{options.ServerCommand} into a WinDbg already attached and choose "
                                          + "Connect to Session");
            }
        }
    }

    private WinDbgInstallation ResolveInstallation()
    {
        if (!string.IsNullOrWhiteSpace(settings.WinDbgPath))
        {
            return File.Exists(settings.WinDbgPath)
                ? WinDbgInstallation.FromExecutable(settings.WinDbgPath)
                : throw new DebuggerException($"WinDbg was not found at {settings.WinDbgPath}. Check the WinDbg Path in Settings");
        }

        return WinDbgInstallation.Locate()
               ?? throw new DebuggerException("WinDbg was not found. Install it from the Microsoft Store, "
                                            + "or set the WinDbg Path in Settings");
    }

    private static string PrepareEngine(WinDbgInstallation installation)
    {
        try
        {
            return installation.PrepareEngine(EngineCacheRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DebuggerException($"The debugger engine could not be copied from {installation.EngineDirectory}: "
                                      + exception.Message);
        }
    }

    private void Launch(string executable, string arguments)
    {
        logger.LogInformation("Starting {Executable} {Arguments}", executable, arguments);

        try
        {
            Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = true, Verb = "runas" });
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ElevationDeclined)
        {
            SetStatus("Not Connected");

            throw new DebuggerException("Elevation was declined. WinDbg has to run as administrator to attach to SQL Server");
        }
    }

    private void SetStatus(string status)
    {
        Status = status;

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}

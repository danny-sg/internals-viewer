using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.Debugging.Exceptions;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.Services.Query.Debugging;

internal sealed class TimeTravelRecording(string ttdDirectory, string traceDirectory, int processId, ILogger logger)
    : ITimeTravelRecording
{
    private const int ElevationDeclined = 1223;

    private const string RecorderLog = "recorder.log";

    private const string ProcessFile = "process.txt";

    private const string HostName = "Time Travel Trace.exe";

    private static readonly string[] Modules = ["sqlmin.dll", "sqllang.dll"];

    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public int ProcessId { get; private set; } = processId;

    public bool Restarted { get; private set; }

    private string Id { get; } = Guid.NewGuid().ToString("N");

    private EventWaitHandle? Prepared { get; set; }

    private EventWaitHandle? Attach { get; set; }

    private EventWaitHandle? Ready { get; set; }

    private EventWaitHandle? Stop { get; set; }

    private Process? Host { get; set; }

    private static string HostExecutable => Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", HostName);

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        Prepared = CreateEvent("Prepared");
        Attach = CreateEvent("Attach");
        Ready = CreateEvent("Ready");
        Stop = CreateEvent("Stop");

        logger.LogInformation("Preparing TTD recording of process {ProcessId} into {Directory}", ProcessId, traceDirectory);

        try
        {
            Host = StartElevated(HostExecutable);
        }
        catch (Win32Exception exception)
        {
            logger.LogWarning("The recorder could not be started from {Path}: {Message}", HostExecutable, exception.Message);

            Host = StartElevated(CopyHost());
        }

        await WaitAsync(() => Prepared.WaitOne(0), PrepareTimeout, "TTD could not prepare SQL Server for recording", cancellationToken);

        var process = (await File.ReadAllTextAsync(Path.Combine(traceDirectory, ProcessFile), cancellationToken))
                          .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        ProcessId = int.Parse(process[0]);

        Restarted = process.Length > 1 && process[1] == "1";

        if (Restarted)
        {
            logger.LogInformation("SQL Server was restarted to clear an earlier TTD recording, now process {ProcessId}", ProcessId);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Attach is null || Ready is null)
        {
            throw new InvalidOperationException("The recording was not prepared");
        }

        Attach.Set();

        await WaitAsync(() => Ready.WaitOne(0) || IsTracing(), StartTimeout, "TTD did not start recording", cancellationToken);
    }

    public async Task<TimeTravelTrace> StopAsync(CancellationToken cancellationToken)
    {
        if (Host is null || Stop is null)
        {
            throw new InvalidOperationException("The recording was not started");
        }

        Stop.Set();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(StopTimeout);

        await Host.WaitForExitAsync(timeout.Token);

        var trace = new DirectoryInfo(traceDirectory).GetFiles("*.run")
                                                     .OrderByDescending(f => f.LastWriteTimeUtc)
                                                     .FirstOrDefault()
                    ?? throw new DebuggerException($"TTD did not write a trace. {ReadLogs()}");

        logger.LogInformation("TTD trace written to {TracePath} ({Size:N0} bytes)", trace.FullName, trace.Length);

        return new TimeTravelTrace(trace.FullName, Path.Combine(ttdDirectory, "TTDReplay.dll"));
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is { HasExited: false } host)
        {
            Stop?.Set();

            using var timeout = new CancellationTokenSource(DisposeTimeout);

            try
            {
                await host.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("TTD recorder for process {ProcessId} did not stop", ProcessId);
            }
        }

        Host?.Dispose();
        Prepared?.Dispose();
        Attach?.Dispose();
        Ready?.Dispose();
        Stop?.Dispose();
    }

    private EventWaitHandle CreateEvent(string purpose)
        => new(false, EventResetMode.ManualReset, EventName(purpose));

    private string EventName(string purpose) => $@"Local\InternalsViewer.TimeTravel.{purpose}.{Id}";

    private async Task WaitAsync(Func<bool> condition, TimeSpan timeout, string failure, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        while (!condition())
        {
            if (Host!.HasExited)
            {
                throw new DebuggerException($"{failure}. {ReadLogs()}");
            }

            if (Stopwatch.GetElapsedTime(started) > timeout)
            {
                throw new DebuggerException($"{failure} within {timeout.TotalMinutes} minutes. {ReadLogs()}");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private Process StartElevated(string executable)
    {
        var modules = string.Join(' ', Modules.Select(m => $"--module {m}"));

        var arguments = $"--ttd \"{ttdDirectory}\" --out \"{traceDirectory}\" --pid {ProcessId} --id {Id} {modules}";

        try
        {
            return Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = true, Verb = "runas" })
                   ?? throw new DebuggerException("The TTD recorder could not be started");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ElevationDeclined)
        {
            throw new DebuggerException("Elevation was declined. TTD has to run as administrator to record SQL Server");
        }
    }

    private string CopyHost()
    {
        var copy = Path.Combine(Path.GetDirectoryName(ttdDirectory)!, HostName);

        File.Copy(HostExecutable, copy, overwrite: true);

        return copy;
    }

    private bool IsTracing()
    {
        try
        {
            return new DirectoryInfo(traceDirectory).GetFiles("*.out")
                                                    .Any(f => File.ReadAllText(f.FullName).Contains("Tracing started"));
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string ReadLogs()
    {
        var text = new StringBuilder();

        foreach (var file in new[] { RecorderLog, "ttd.error.log", "ttd.log" })
        {
            var path = Path.Combine(traceDirectory, file);

            try
            {
                if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } content)
                {
                    text.AppendLine(content);
                }
            }
            catch (IOException exception)
            {
                logger.LogDebug("{Path} could not be read: {Message}", path, exception.Message);
            }
        }

        return text.Length > 0 ? text.ToString().Trim() : $"See the logs in {traceDirectory}";
    }
}

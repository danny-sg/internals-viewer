using InternalsViewer.Query.Debugging.Exceptions;
using InternalsViewer.Query.Debugging.Interfaces;
using System.Diagnostics;
using System.Text;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// Drives a debugger session that lives in a <see cref="DebuggerHost"/> process
/// </summary>
/// <remarks>
/// One request is in flight at a time. A reply that does not arrive within <see cref="RequestTimeout"/>, or a host that exits, ends the
/// session with the host's error output in the message.
/// </remarks>
public sealed class DebuggerHostClient : IDebuggerSession
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(3);

    private readonly StringBuilder _errors = new();

    private readonly SemaphoreSlim _gate = new(1, 1);

    private DebuggerHostClient(Process process)
    {
        Process = process;

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_errors)
                {
                    _errors.AppendLine(e.Data);
                }
            }
        };

        process.BeginErrorReadLine();
    }

    private Process Process { get; }

    /// <summary>
    /// Starts a host process
    /// </summary>
    /// <remarks>
    /// A failure to start surfaces on the first request
    /// </remarks>
    public static DebuggerHostClient Start(string executable, string arguments)
    {
        var info = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        var process = Process.Start(info) ?? throw new DebuggerException($"The debugger host {executable} did not start");

        return new DebuggerHostClient(process);
    }

    public Task ConnectAsync(string engine, string remoteOptions, CancellationToken cancellationToken) =>
        RequestAsync($"connect\t{engine}\t{remoteOptions}", cancellationToken);

    public Task SendAsync(string command, CancellationToken cancellationToken) =>
        RequestAsync($"send\t{command}", cancellationToken);

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.StandardInput.WriteLine("quit");

                Process.StandardInput.Flush();

                if (!Process.WaitForExit(QuitTimeout))
                {
                    Process.Kill();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
        finally
        {
            Process.Dispose();

            _gate.Dispose();
        }
    }

    private async Task RequestAsync(string request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (Process.HasExited)
            {
                throw new DebuggerException($"The debugger host has exited{ErrorOutput()}");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(RequestTimeout);

            await Process.StandardInput.WriteLineAsync(request.AsMemory(), timeout.Token);

            await Process.StandardInput.FlushAsync(timeout.Token);

            var reply = await Process.StandardOutput.ReadLineAsync(timeout.Token)
                        ?? throw new DebuggerException($"The debugger host has exited{ErrorOutput()}");

            var parts = reply.Split('\t', 3);

            if (parts[0] == "ok")
            {
                return;
            }

            if (parts is ["error", "engine", var engineMessage])
            {
                throw new DebuggerException(engineMessage, isEngineFailure: true);
            }

            throw new DebuggerException(parts.Length == 3 ? parts[2] : $"Unexpected reply from the debugger host: {reply}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DebuggerException($"The debugger host did not reply within {RequestTimeout.TotalSeconds:0} seconds");
        }
        catch (IOException exception)
        {
            throw new DebuggerException($"The debugger host connection failed: {exception.Message}{ErrorOutput()}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ErrorOutput()
    {
        lock (_errors)
        {
            return _errors.Length == 0 ? string.Empty : $": {_errors.ToString().Trim()}";
        }
    }
}

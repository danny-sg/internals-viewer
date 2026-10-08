using InternalsViewer.Query.Debugging.Exceptions;
using InternalsViewer.Query.Debugging.Interfaces;
using System.Runtime.InteropServices;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// Serves a debugger session to another process over a line-based request/response stream
/// </summary>
/// <remarks>
/// The engine runs in its own process because it cannot share one with the app: the app already has an older <c>dbghelp.dll</c> loaded for
/// symbol resolution, and Windows binds the engine's imports to whichever copy loaded first. A process of its own also keeps a debugger
/// engine fault away from the app.
///
/// Requests are tab separated, one per line. Each is answered with <c>ok</c>, or <c>error\t{kind}\t{message}</c> where the kind is
/// <c>engine</c> when the engine could not be loaded and <c>session</c> otherwise.
/// </remarks>
public static class DebuggerHost
{
    public const string Switch = "--windbg-host";

    private const uint FailCriticalErrors = 0x0001;
    private const uint NoOpenFileErrorBox = 0x8000;

    public static async Task<int> RunAsync(TextReader input,
                                           TextWriter output,
                                           Func<string, string, CancellationToken, Task<IDebuggerSession>> connect)
    {
        if (OperatingSystem.IsWindows())
        {
            SetErrorMode(FailCriticalErrors | NoOpenFileErrorBox);
        }

        IDebuggerSession? session = null;

        try
        {
            while (await input.ReadLineAsync() is { } line)
            {
                var parts = line.Split('\t');

                try
                {
                    switch (parts[0])
                    {
                        case "connect" when parts.Length == 3:
                            session?.Dispose();

                            session = await connect(parts[1], parts[2], CancellationToken.None);

                            break;

                        case "send" when parts.Length == 2 && session is not null:
                            await session.SendAsync(parts[1], CancellationToken.None);

                            break;

                        case "send":
                            throw new DebuggerException("No debugger session is connected");

                        case "quit":
                            return 0;

                        default:
                            throw new DebuggerException($"Unknown request {parts[0]}");
                    }

                    await output.WriteLineAsync("ok");
                }
                catch (Exception exception)
                {
                    var kind = exception is DebuggerException { IsEngineFailure: true } ? "engine" : "session";

                    await output.WriteLineAsync($"error\t{kind}\t{Flatten(exception.Message)}");
                }

                await output.FlushAsync();
            }

            return 0;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static string Flatten(string message) => message.ReplaceLineEndings(" ").Replace('\t', ' ');

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}

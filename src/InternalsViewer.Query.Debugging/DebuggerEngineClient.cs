using InternalsViewer.Query.Debugging.Exceptions;
using InternalsViewer.Query.Debugging.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.Debugging;

/// <summary>
/// A client of a running debugger session, connected over the debugger engine's remote protocol
/// </summary>
/// <remarks>
/// <c>dbgeng.dll</c> is loaded by name at connect time, so nothing is needed until a session is used and a machine
/// without the debugging tools still runs everything else. The interfaces are called through their vtables directly
/// rather than through COM interop, with slot numbers taken from <c>dbgeng.h</c> in the Windows SDK. The engine ties
/// a client to the thread that created it, so every call is marshalled onto one dedicated thread.
/// </remarks>
public sealed class DebuggerEngineClient : IDebuggerSession
{
    private const uint StatusBreak = 6;
    private const uint StatusNoDebuggee = 7;
    private const uint InterruptActive = 0;
    private const uint OutputControlAllClients = 1;
    private const uint ExecuteEcho = 1;

    private const string CommandBanner = ".echo Internals Viewer:";

    private const int QueryInterfaceSlot = 0;
    private const int ReleaseSlot = 2;
    private const int SetInterruptSlot = 4;
    private const int GetExecutionStatusSlot = 49;
    private const int ExecuteSlot = 66;

    private static readonly Guid DebugClientId = new("27fe5639-8407-4f47-8364-ee118fb08ac8");

    private static readonly Guid DebugControlId = new("5182e668-105e-416e-ad92-24ef800424ba");

    private static readonly TimeSpan BreakTimeout = TimeSpan.FromSeconds(10);

    private const DllImportSearchPath LibrarySearchPath =
        DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories;

    private nint _client;
    private nint _control;

    private DebuggerEngineClient(DebuggerThread thread, nint client, nint control)
    {
        Thread = thread;

        _client = client;
        _control = control;
    }

    private DebuggerThread Thread { get; }

    /// <summary>
    /// Connects to a debugger listening on the given remote options
    /// </summary>
    public static async Task<DebuggerEngineClient> ConnectAsync(string remoteOptions,
                                                        string? libraryPath,
                                                        CancellationToken cancellationToken)
    {
        var thread = new DebuggerThread();

        try
        {
            return await thread.RunAsync(() => Connect(thread, remoteOptions, libraryPath), cancellationToken);
        }
        catch
        {
            thread.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Runs a command in the session
    /// </summary>
    /// <remarks>
    /// Breaks in first and resumes afterwards if the target is running. The command's output stays in the debugger's own
    /// window, which is the primary place commands are read.
    /// </remarks>
    public Task SendAsync(string command, CancellationToken cancellationToken) =>
        Thread.RunAsync(() =>
        {
            Send(command);

            return true;
        }, cancellationToken);

    public void Dispose()
    {
        if (_control == 0 && _client == 0)
        {
            return;
        }

        try
        {
            Thread.RunAsync(() =>
            {
                Release(_control);
                Release(_client);

                _control = 0;
                _client = 0;

                return true;
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Thread.Dispose();
        }
    }

    private static unsafe DebuggerEngineClient Connect(DebuggerThread thread, string remoteOptions, string? libraryPath)
    {
        var library = LoadLibrary(libraryPath);

        if (!NativeLibrary.TryGetExport(library, "DebugConnectWide", out var export))
        {
            throw new DebuggerException($"{libraryPath} is not a debugger engine: it has no DebugConnectWide", isEngineFailure: true);
        }

        var connect = (delegate* unmanaged<char*, Guid*, void**, int>)export;

        var clientId = DebugClientId;

        void* client;

        int result;

        fixed (char* options = remoteOptions)
        {
            result = connect(options, &clientId, &client);
        }

        if (result < 0)
        {
            throw new DebuggerException($"No debugger session was found at {remoteOptions} (0x{result:X8})");
        }

        var controlId = DebugControlId;

        void* control;

        result = ((delegate* unmanaged<void*, Guid*, void**, int>)Slot((nint)client, QueryInterfaceSlot))(client, &controlId, &control);

        if (result < 0)
        {
            Release((nint)client);

            throw new DebuggerException($"The debugger did not provide a control interface (0x{result:X8})");
        }

        return new DebuggerEngineClient(thread, (nint)client, (nint)control);
    }

    private static nint LoadLibrary(string? libraryPath)
    {
        var path = string.IsNullOrWhiteSpace(libraryPath) ? "dbgeng.dll" : libraryPath;

        try
        {
            return NativeLibrary.Load(path, typeof(DebuggerEngineClient).Assembly, LibrarySearchPath);
        }
        catch (DllNotFoundException exception)
        {
            throw new DebuggerException($"{path} could not be loaded ({exception.Message}). Install WinDbg or the "
                                        + "Debugging Tools for Windows, or set the WinDbg path in Settings",
                                        isEngineFailure: true);
        }
    }

    private void Send(string command)
    {
        var status = GetExecutionStatus();

        if (status == StatusNoDebuggee)
        {
            throw new DebuggerException("The debugger has no process attached");
        }

        var running = status != StatusBreak;

        if (running)
        {
            BreakIn();
        }

        Execute(CommandBanner);

        Execute(command);

        if (running)
        {
            Execute("g");
        }
    }

    private unsafe void BreakIn()
    {
        Check(((delegate* unmanaged<void*, uint, int>)Slot(_control, SetInterruptSlot))((void*)_control, InterruptActive),
              "Breaking into the target failed");

        var started = Stopwatch.GetTimestamp();

        while (GetExecutionStatus() != StatusBreak)
        {
            if (Stopwatch.GetElapsedTime(started) > BreakTimeout)
            {
                throw new DebuggerException($"The target did not break in within {BreakTimeout.TotalSeconds:0} seconds");
            }

            System.Threading.Thread.Sleep(50);
        }
    }

    private unsafe uint GetExecutionStatus()
    {
        uint status;

        Check(((delegate* unmanaged<void*, uint*, int>)Slot(_control, GetExecutionStatusSlot))((void*)_control, &status),
              "Reading the debugger's execution status failed");

        return status;
    }

    private unsafe void Execute(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command + "\0");

        fixed (byte* text = bytes)
        {
            Check(((delegate* unmanaged<void*, uint, byte*, uint, int>)Slot(_control, ExecuteSlot))((void*)_control,
                                                                                                    OutputControlAllClients,
                                                                                                    text,
                                                                                                    ExecuteEcho),
                  $"The debugger rejected the command {command}");
        }
    }

    private static unsafe void Release(nint instance)
    {
        if (instance != 0)
        {
            ((delegate* unmanaged<void*, uint>)Slot(instance, ReleaseSlot))((void*)instance);
        }
    }

    private static unsafe void* Slot(nint instance, int index) => (*(void***)instance)[index];

    private static void Check(int result, string message)
    {
        if (result < 0)
        {
            throw new DebuggerException($"{message} (0x{result:X8})");
        }
    }
}

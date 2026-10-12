using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.Debugging.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Markers;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel;

/// <summary>
/// Time-Travel Debug Read Session
/// </summary>
/// <remarks>
/// This is responsible for opening a TTD .run trace file and replaying the trace via the Time Travel Bridge as an interface for the Time
/// Travel Debugging API.
///
/// OpenAsync
///
///     Opens a TTD .run trace file and returns a TimeTravelSession instance for replaying the trace. This requires Initialize() to be
///     called in the API that indexes the .run file ready for reading.
///
///     The Bridge also provides mappings of modules including start address and length, that allows an absolute address to be mapped to a
///     module and RVA which is required for symbol resolution.
///
/// ReplayAsync
///
///     Replays the trace once with a single API cursor, a forward pass from the first position to the end of the trace, keeping only the
///     calls made on the specified thread Ids. A thread's progress is how far the pass has got through that thread's active range.
///
///     The forward pass is the most efficient way of reading the trace.
///
///     Delegates are used to add to the call log, timeline, and memory locators to keep memory usage low and avoid large allocations.
///
///     Returns a TimeTravelReplay with the raw call nodes, the modules, the call log and the timeline.
/// </remarks>
internal sealed class TimeTravelSession : IDisposable
{
    private const int NameLength = 1024;

    private TimeTravelSession(TimeTravelTraceFile trace, TimeTravelTraceHandle handle)
    {
        TraceFile = trace;
        Handle = handle;
        Modules = ReadModules(handle);
    }

    public TimeTravelTraceFile TraceFile { get; }

    public IReadOnlyList<TimeTravelModule> Modules { get; }

    private TimeTravelTraceHandle Handle { get; }

    /// <summary>
    /// Semaphore to gate access to the replay engine
    /// </summary>
    /// <remarks>
    /// Only one replay runs on a session at a time, each replay being a full forward pass of the trace.
    /// </remarks>
    private SemaphoreSlim Gate { get; } = new(1, 1);

    public static Task<TimeTravelSession> OpenAsync(TimeTravelTraceFile trace, CancellationToken cancellationToken)
        => Task.Run(() => Open(trace), cancellationToken);

    public async Task<TimeTravelReplay> ReplayAsync(IReadOnlyCollection<uint> threadIds,
                                                    ReplayFunctionSet functions,
                                                    IProgress<double>? progress,
                                                    CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => Replay([.. threadIds], functions, progress, cancellationToken), cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public void Dispose() => Handle.Dispose();

    private static TimeTravelSession Open(TimeTravelTraceFile trace)
    {
        NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,
                                        "runtimes",
                                        "win-x64",
                                        "native",
                                        "InternalsViewer.Query.TimeTravelBridge.dll"));

        var result = TimeTravelBridge.OpenTrace(trace.ReplayLibraryPath, trace.Path, out var handle);

        if (result != 0)
        {
            handle.Dispose();

            throw new InvalidOperationException(Describe(result, trace));
        }

        return new TimeTravelSession(trace, handle);
    }

    private TimeTravelReplay Replay(uint[] threads,
                                    ReplayFunctionSet functions,
                                    IProgress<double>? progress,
                                    CancellationToken cancellationToken)
    {
        var cancel = Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(cancel, 0);

        var registration = cancellationToken.Register(() => Marshal.WriteInt32(cancel, 1));

        var percentages = new Dictionary<uint, int>();

        TimeTravelBridge.ProgressCallback onProgress = (thread, percent) =>
        {
            percentages[thread] = percent;

            progress?.Report(percentages.Values.Average());
        };

        var log = new TimeTravelCallLog.Builder(functions.Locators);

        var timeline = new TimeTravelTimeline.Builder();

        TimeTravelBridge.CallChunkCallback onChunk = log.Add;

        TimeTravelBridge.CallSpanCallback onSpans = timeline.Add;

        TimeTravelBridge.PointerValueCallback onPointerValues = log.AddLocators;

        var markers = new TimeTravelMarkerLog();

        TimeTravelBridge.MarkerCallback onMarkers = markers.Add;

        ulong[] publishers = [.. functions.Publishers.Select(p => p.Address)];

        TimeTravelPointerRead[] reads = [.. functions.Locators.Select(TimeTravelPointerRead.From)];

        ulong[] instanceMethods = [.. functions.InstanceMethods.Select(TimeTravelBridge.PackInstanceMethod)];

        try
        {
            var result = TimeTravelBridge.ReadCallTree(Handle,
                                                       threads,
                                                       threads.Length,
                                                       instanceMethods,
                                                       instanceMethods.Length,
                                                       functions.Excluded,
                                                       functions.Excluded.Length,
                                                       reads,
                                                       reads.Length,
                                                       publishers,
                                                       publishers.Length,
                                                       functions.Reserves,
                                                       functions.Reserves.Length,
                                                       onChunk,
                                                       onSpans,
                                                       onPointerValues,
                                                       onMarkers,
                                                       onProgress,
                                                       cancel,
                                                       out var tree);

            // Stops the delegates from being GCed while the replay is in progress
            GC.KeepAlive(onChunk);

            GC.KeepAlive(onSpans);

            GC.KeepAlive(onPointerValues);

            GC.KeepAlive(onMarkers);

            GC.KeepAlive(onProgress);

            if (result != 0)
            {
                throw new InvalidOperationException(Describe(result, TraceFile));
            }

            try
            {
                var nodes = new TimeTravelCallNode[TimeTravelBridge.GetCallNodeCount(tree)];

                TimeTravelBridge.GetCallNodes(tree, nodes, nodes.Length);

                cancellationToken.ThrowIfCancellationRequested();

                return new TimeTravelReplay(nodes, Modules, log.Build(), timeline.Build(nodes), markers.Build());
            }
            finally
            {
                TimeTravelBridge.CloseCallTree(tree);
            }
        }
        finally
        {
            registration.Dispose();

            Marshal.FreeHGlobal(cancel);
        }
    }

    private static TimeTravelModule[] ReadModules(TimeTravelTraceHandle handle)
    {
        var count = TimeTravelBridge.GetModuleCount(handle);

        var modules = new List<TimeTravelModule>(count);

        var name = new StringBuilder(NameLength);

        for (var i = 0; i < count; i++)
        {
            if (TimeTravelBridge.GetModule(handle, i, name, name.Capacity, out var address, out var size))
            {
                modules.Add(TimeTravelModule.Read(name.ToString(), address, size));
            }
        }

        return [.. modules];
    }

    private static string Describe(int result, TimeTravelTraceFile trace) => result switch
    {
        1
            => $"The TTD replay engine could not be loaded from {trace.ReplayLibraryPath}",
        2
            => $"{trace.ReplayLibraryPath} does not export CreateReplayEngine",
        3
            => "The TTD replay engine could not be created. The replay engine may not match the API version",
        4
            => $"The time travel trace {trace.Path} could not be opened",
        5
            => "The TTD replay engine could not create a cursor",
        6
            => "The time travel trace could not be replayed",
        7
            => "Replay of the time travel trace was cancelled",
        _
            => $"Reading the time travel trace failed with code {result}"
    };
}

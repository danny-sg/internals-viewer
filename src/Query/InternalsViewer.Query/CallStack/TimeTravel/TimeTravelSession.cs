using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.Query.Debugging.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed class TimeTravelSession : IDisposable
{
    private const int NameLength = 1024;

    private const int ActivitySlices = 1024;

    private TimeTravelSession(TimeTravelTrace trace, TimeTravelTraceHandle handle)
    {
        Trace = trace;
        Handle = handle;
        Modules = ReadModules(handle);
    }

    public TimeTravelTrace Trace { get; }

    public IReadOnlyList<TimeTravelModule> Modules { get; }

    private TimeTravelTraceHandle Handle { get; }

    private SemaphoreSlim Gate { get; } = new(1, 1);

    public static Task<TimeTravelSession> OpenAsync(TimeTravelTrace trace, CancellationToken cancellationToken)
        => Task.Run(() => Open(trace), cancellationToken);

    public async Task<TimeTravelReplay> ReplayAsync(IReadOnlyCollection<uint> threadIds,
                                                    ReplayFunctionSet functions,
                                                    IProgress<ProgressDetail>? progress,
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

    private static TimeTravelSession Open(TimeTravelTrace trace)
    {
        NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,
                                        "runtimes",
                                        "win-x64",
                                        "native",
                                        "InternalsViewer.Query.TimeTravelBridge.dll"));

        var result = TimeTravelBridge.OpenTrace(trace.ReplayLibraryPath, trace.TracePath, out var handle);

        if (result != 0)
        {
            handle.Dispose();

            throw new InvalidOperationException(Describe(result, trace));
        }

        return new TimeTravelSession(trace, handle);
    }

    private TimeTravelReplay Replay(uint[] threads,
                                    ReplayFunctionSet functions,
                                    IProgress<ProgressDetail>? progress,
                                    CancellationToken cancellationToken)
    {
        var cancel = Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(cancel, 0);

        var registration = cancellationToken.Register(() => Marshal.WriteInt32(cancel, 1));

        TimeTravelBridge.ProgressCallback onProgress = (thread, percent) =>
            progress?.Report(new ProgressDetail(thread == 0 ? "Replaying Full Trace" : $"[Thread {thread}] Replaying Full Trace", percent));

        var log = new TimeTravelCallLog.Builder();

        var timeline = new TimeTravelTimeline.Builder();

        TimeTravelBridge.CallChunkCallback onChunk = log.Add;

        TimeTravelBridge.CallSpanCallback onSpans = timeline.Add;

        try
        {
            var result = TimeTravelBridge.ReadCallTree(Handle,
                                                       threads,
                                                       threads.Length,
                                                       functions.InstanceMethods,
                                                       functions.InstanceMethods.Length,
                                                       functions.Excluded,
                                                       functions.Excluded.Length,
                                                       functions.Markers,
                                                       functions.Markers.Length,
                                                       ActivitySlices,
                                                       onChunk,
                                                       onSpans,
                                                       onProgress,
                                                       cancel,
                                                       out var tree);

            GC.KeepAlive(onChunk);

            GC.KeepAlive(onSpans);

            GC.KeepAlive(onProgress);

            if (result != 0)
            {
                throw new InvalidOperationException(Describe(result, Trace));
            }

            try
            {
                var nodes = new TimeTravelCallNode[TimeTravelBridge.GetCallNodeCount(tree)];

                TimeTravelBridge.GetCallNodes(tree, nodes, nodes.Length);

                var activity = new TimeTravelCallActivity[TimeTravelBridge.GetCallActivityCount(tree)];

                TimeTravelBridge.GetCallActivity(tree, activity, activity.Length);

                cancellationToken.ThrowIfCancellationRequested();

                return new TimeTravelReplay(new TimeTravelCallTree(nodes, activity, [.. Modules]), log.Build(), timeline.Build(nodes));
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
                modules.Add(new TimeTravelModule(name.ToString(), address, size));
            }
        }

        return [.. modules];
    }

    private static string Describe(int result, TimeTravelTrace trace) => result switch
    {
        1
            => $"The TTD replay engine could not be loaded from {trace.ReplayLibraryPath}",
        2
            => $"{trace.ReplayLibraryPath} does not export CreateReplayEngine",
        3
            => "The TTD replay engine could not be created. The replay engine may not match the API version",
        4
            => $"The time travel trace {trace.TracePath} could not be opened",
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

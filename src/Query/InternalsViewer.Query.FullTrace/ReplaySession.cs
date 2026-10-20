using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.Stubs;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Time-Travel Debug Read Session
/// </summary>
/// <remarks>
/// This is responsible for opening a TTD .run trace file and replaying the trace via the Time Travel Bridge as an interface for the Time
/// Travel Debugging API.
///
/// OpenAsync
///
///     Opens a TTD .run trace file and returns a ReplaySession instance for replaying the trace. This requires Initialize() to be
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
///     Returns a ReplayResult with the raw call nodes, the modules, the call log and the timeline.
/// </remarks>
internal sealed class ReplaySession : IDisposable
{
    private const int NameLength = 1024;

    private ReplaySession(TimeTravelTraceFile trace, TraceHandle handle)
    {
        TraceFile = trace;
        Handle = handle;
        Modules = ReadModules(handle);
    }

    public TimeTravelTraceFile TraceFile { get; }

    public IReadOnlyList<TraceModule> Modules { get; }

    private TraceHandle Handle { get; }

    /// <summary>
    /// Semaphore to gate access to the replay engine
    /// </summary>
    /// <remarks>
    /// Only one replay runs on a session at a time, each replay being a full forward pass of the trace.
    /// </remarks>
    private SemaphoreSlim Gate { get; } = new(1, 1);

    public static Task<ReplaySession> OpenAsync(TimeTravelTraceFile trace, CancellationToken cancellationToken)
        => Task.Run(() => Open(trace), cancellationToken);

    public async Task<ReplayResult> ReplayAsync(IReadOnlyCollection<uint> threadIds,
                                                ReplayFunctions functions,
                                                IReadOnlyList<CustomHookDefinition> customHooks,
                                                IProgress<double>? progress,
                                                CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => Replay([.. threadIds], functions, customHooks, progress, cancellationToken), cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public void Dispose() => Handle.Dispose();

    private static ReplaySession Open(TimeTravelTraceFile trace)
    {
        NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,
                                        "runtimes",
                                        "win-x64",
                                        "native",
                                        "InternalsViewer.Query.TimeTravelBridge.dll"));

        var result = ReplayBridge.OpenTrace(trace.ReplayLibraryPath, trace.Path, out var handle);

        if (result != 0)
        {
            handle.Dispose();

            throw new InvalidOperationException(Describe(result, trace));
        }

        return new ReplaySession(trace, handle);
    }

    private ReplayResult Replay(uint[] threads,
                                ReplayFunctions functions,
                                IReadOnlyList<CustomHookDefinition> customHooks,
                                IProgress<double>? progress,
                                CancellationToken cancellationToken)
    {
        var cancel = Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(cancel, 0);

        var registration = cancellationToken.Register(() => Marshal.WriteInt32(cancel, 1));

        var percentages = new Dictionary<uint, int>();

        ReplayBridge.ProgressCallback onProgress = (thread, percent) =>
        {
            percentages[thread] = percent;

            progress?.Report(percentages.Values.Average());
        };

        using var images = new ImageText(Modules);

        var store = new TraceCallStore.Builder();

        var timeline = new TraceTimeline.Builder();

        ReplayBridge.CallChunkCallback onChunk = store.Add;

        ReplayBridge.CallEndCallback onEnd = store.AddToPosition;

        ReplayBridge.CallSpanCallback onSpans = timeline.Add;

        ulong[] instanceMethods = [.. functions.InstanceMethods.Select(ReplayBridge.PackInstanceMethod)];

        MemoryMapFunction[] memoryMapFunctions = [.. functions.MemoryMapFunctions,
                                                  .. functions.MemoryFunctions
                                                              .Where(f => f is { IsAllocation: true, IsVirtual: false })
                                                              .Select(f => new MemoryMapFunction(f.Address, MemoryMapOperation.Allocated))];

        var memoryMap = new MemoryMapHook(memoryMapFunctions);

        var rowSources = new RowSourceHook(functions.RowSources);

        var output = new OutputHook(functions.Output);

        var customHook = new CustomHook(Resolve(customHooks));

        var hooks = new TraceHooks([rowSources,
                                    new LocatorHook(functions.LocatorReads, store, images),
                                    memoryMap,
                                    output,
                                    customHook]);

        ReplayBridge.HookCallback onHook = hooks.OnHook;

        var stubs = new StubResolver(images, Modules);

        ReplayBridge.StubCallback onStub = stubs.Resolve;

        try
        {
            var result = ReplayBridge.ReadCallTree(Handle,
                                                   threads,
                                                   threads.Length,
                                                   instanceMethods,
                                                   instanceMethods.Length,
                                                   functions.ExcludedFunctions,
                                                   functions.ExcludedFunctions.Length,
                                                   hooks.Functions,
                                                   hooks.Functions.Length,
                                                   onChunk,
                                                   onEnd,
                                                   onSpans,
                                                   onHook,
                                                   onStub,
                                                   onProgress,
                                                   cancel,
                                                   out var tree);

            // Stops the delegates from being GCed while the replay is in progress
            GC.KeepAlive(onChunk);

            GC.KeepAlive(onEnd);

            GC.KeepAlive(onSpans);

            GC.KeepAlive(onHook);

            GC.KeepAlive(onStub);

            GC.KeepAlive(onProgress);

            if (result != 0)
            {
                throw new InvalidOperationException(Describe(result, TraceFile));
            }

            try
            {
                var nodes = new CallNode[ReplayBridge.GetCallNodeCount(tree)];

                ReplayBridge.GetCallNodes(tree, nodes, nodes.Length);

                cancellationToken.ThrowIfCancellationRequested();

                if (hooks.Failure is { } failure)
                {
                    throw new InvalidOperationException("A trace hook failed while the trace was replayed", failure);
                }

                if (stubs.Failure is { } stubFailure)
                {
                    throw new InvalidOperationException("A call stub failed to resolve while the trace was replayed", stubFailure);
                }

                return new ReplayResult(nodes,
                                        Modules,
                                        store.Build(),
                                        timeline.Build(nodes),
                                        memoryMap.Build(),
                                        customHook.Build(),
                                        output.Build(),
                                        rowSources.GetPlanNodes(),
                                        rowSources.GetTeardowns());
            }
            finally
            {
                ReplayBridge.CloseCallTree(tree);
            }
        }
        finally
        {
            registration.Dispose();

            Marshal.FreeHGlobal(cancel);
        }
    }

    private IEnumerable<(ulong Address, CustomHookDefinition Definition)> Resolve(IReadOnlyList<CustomHookDefinition> customHooks)
        => customHooks.SelectMany(h => Modules.Where(m => m.Name.Equals(h.Module, StringComparison.OrdinalIgnoreCase)
                                                          && m.Guid.Equals(h.Guid, StringComparison.OrdinalIgnoreCase)
                                                          && m.Age == h.Age)
                                              .Select(m => (m.StartAddress + h.Rva, h)));

    private static TraceModule[] ReadModules(TraceHandle handle)
    {
        var count = ReplayBridge.GetModuleCount(handle);

        var modules = new List<TraceModule>(count);

        var name = new StringBuilder(NameLength);

        for (var i = 0; i < count; i++)
        {
            if (ReplayBridge.GetModule(handle, i, name, name.Capacity, out var address, out var size))
            {
                modules.Add(TraceModule.Read(name.ToString(), address, size));
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

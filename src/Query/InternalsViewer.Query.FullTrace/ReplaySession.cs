using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Native.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Stubs;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

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
///     Callbacks add to the call log and timeline as the pass runs, and the hooks read memory at the calls they watch, to keep memory
///     usage low and avoid large allocations.
///
///     Returns a ReplayResult with the raw call nodes, the modules, the call log, the timeline and what the hooks collected: memory map
///     entries, custom hook captures, output addresses, plan nodes and query teardowns.
///
/// WatchAsync
///
///     Replays the trace again between two positions with write watchpoints on the given address ranges, keeping the hits the caller
///     selects together with the bytes written. Memory History runs it for the ranges it shows.
/// </remarks>
internal sealed class ReplaySession : IDisposable
{
    private const int NameLength = 1024;

    private const uint ReadAccess = 1;

    private const uint WriteAccess = 2;

    private const uint ReadType = 0;

    private const string ServerModule = "sqlservr";

    private ReplaySession(TimeTravelTraceFile trace, TraceHandle handle)
    {
        TraceFile = trace;
        Handle = handle;
        Modules = ReadModules(handle);
        ServerVersion = Modules.FirstOrDefault(m => string.Equals(m.Name, ServerModule, StringComparison.OrdinalIgnoreCase))
                               ?.ProductVersion;
        Layout = EngineLayout.Get(ServerVersion);
    }

    public TimeTravelTraceFile TraceFile { get; }

    public IReadOnlyList<TraceModule> Modules { get; }

    public Version? ServerVersion { get; }

    internal EngineLayout Layout { get; }

    private TraceHandle Handle { get; }

    /// <summary>
    /// Semaphore to gate access to the replay engine
    /// </summary>
    /// <remarks>
    /// Only one pass runs on a session at a time, whether a replay of the whole trace or a memory watch over part of it.
    /// </remarks>
    private SemaphoreSlim Gate { get; } = new(1, 1);

    public static Task<ReplaySession> OpenAsync(TimeTravelTraceFile trace, CancellationToken cancellationToken)
        => Task.Run(() => Open(trace), cancellationToken);

    public async Task<ReplayResult> ReplayAsync(IReadOnlyCollection<uint> threadIds,
                                                ReplayFunctionTable functions,
                                                bool isStackCaptured,
                                                IProgress<double>? progress,
                                                CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => Replay([.. threadIds], functions, isStackCaptured, progress, cancellationToken),
                                  cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<StepReplayOutcome> ReplayAsync(uint thread,
                                                     TracePosition from,
                                                     TracePosition to,
                                                     ulong entryAddress,
                                                     long maximumSteps,
                                                     List<ITraceHook> hooks,
                                                     IProgress<double>? progress,
                                                     CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => ReplaySteps(thread, from, to, entryAddress, maximumSteps, hooks, progress, cancellationToken),
                                  cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<WatchResult> WatchAsync(IReadOnlyCollection<WatchRange> ranges,
                                              TracePosition start,
                                              TracePosition end,
                                              Func<WatchHit, bool> keep,
                                              bool isWatchingWrites,
                                              Action<WatchHit>? onRead,
                                              IProgress<double>? progress,
                                              CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => Watch([.. ranges], start, end, keep, isWatchingWrites, onRead, progress, cancellationToken),
                                  cancellationToken);
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
                                ReplayFunctionTable functions,
                                bool isStackCaptured,
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

        var operations = AddressMapHook.GetOperations(functions.AddressMapFunctions);

        var addressMap = new AddressMapHook(operations,
                                            functions.AllocationFunctions
                                                     .Where(f => f is { IsAllocation: true, IsVirtual: false })
                                                     .Select(f => new AddressMapFunction(f.Address, AddressMapOperation.Allocated)),
                                            VtableResolver.GetUniqueClasses(functions.Vtables));

        var planNodeHook = new PlanNodeHook(functions.PlanNodeFunctions, functions.PlanObjects);

        var bufferPool = new BufferPoolHook(functions.BufferPoolFunctions);

        var stack = isStackCaptured ? new StackSnapshotHook(operations) : null;

        var hooks = new TraceHookDispatcher([planNodeHook,
                                    new LocatorHook(functions.LocatorReads, store, images),
                                    new StackArgumentHook(functions.StackArguments, store),
                                    addressMap,
                                    bufferPool,
                                    .. stack is null ? Array.Empty<ITraceHook>() : [stack]],
                                   Layout);

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
                                        addressMap.Build(),
                                        bufferPool.Build(),
                                        planNodeHook.GetPlanNodes())
                {
                    Buffers = bufferPool.GetBuffers(),
                    StackSnapshotResult = stack?.Build() ?? StackSnapshotResult.Empty
                };
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

    private unsafe WatchResult Watch(WatchRange[] ranges,
                                     TracePosition start,
                                     TracePosition end,
                                     Func<WatchHit, bool> keep,
                                     bool isWatchingWrites,
                                     Action<WatchHit>? onRead,
                                     IProgress<double>? progress,
                                     CancellationToken cancellationToken)
    {
        var cancel = Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(cancel, 0);

        var registration = cancellationToken.Register(() => Marshal.WriteInt32(cancel, 1));

        var chunks = new List<DataChunk<ChunkHit>>();

        var received = 0L;

        var selected = Array.Empty<bool>();

        ReplayBridge.WatchHitCallback onHits = (pointer, count, bytes, length) =>
        {
            var source = new ReadOnlySpan<WatchHit>((void*)pointer, count);

            var chunk = new ReadOnlySpan<byte>((void*)bytes, length);

            if (selected.Length < count)
            {
                selected = new bool[count];
            }

            var hitCount = 0;

            var byteCount = 0;

            for (var index = 0; index < count; index++)
            {
                if (source[index].Type == ReadType)
                {
                    onRead?.Invoke(source[index]);

                    selected[index] = false;

                    continue;
                }

                received++;

                selected[index] = keep(source[index]);

                if (selected[index])
                {
                    hitCount++;

                    byteCount += (int)source[index].Size;
                }
            }

            if (hitCount == 0)
            {
                return;
            }

            var hits = new ChunkHit[hitCount];

            var data = new byte[byteCount];

            var kept = 0;

            var offset = 0;

            for (var index = 0; index < count; index++)
            {
                if (!selected[index])
                {
                    continue;
                }

                var hit = source[index];

                chunk.Slice((int)hit.Offset, (int)hit.Size).CopyTo(data.AsSpan(offset));

                hits[kept++] = ChunkHit.Create(hit, offset);

                offset += (int)hit.Size;
            }

            chunks.Add(new DataChunk<ChunkHit>(hits, data));
        };

        ReplayBridge.ProgressCallback onProgress = (_, percent) => progress?.Report(percent);

        try
        {
            var result = ReplayBridge.WatchMemory(Handle,
                                                  ranges,
                                                  ranges.Length,
                                                  (isWatchingWrites ? WriteAccess : 0) | (onRead is null ? 0 : ReadAccess),
                                                  start.Sequence,
                                                  end.IsKnown ? end.Sequence + 1 : 0,
                                                  onHits,
                                                  onProgress,
                                                  cancel,
                                                  out var watched);

            GC.KeepAlive(onHits);

            GC.KeepAlive(onProgress);

            if (result != 0)
            {
                throw new InvalidOperationException(Describe(result, TraceFile));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new WatchResult([.. chunks], watched, received);
        }
        finally
        {
            registration.Dispose();

            Marshal.FreeHGlobal(cancel);
        }
    }

    private StepReplayOutcome ReplaySteps(uint thread,
                                          TracePosition from,
                                          TracePosition to,
                                          ulong entryAddress,
                                          long maximumSteps,
                                          List<ITraceHook> hooks,
                                          IProgress<double>? progress,
                                          CancellationToken cancellationToken)
    {
        var cancel = Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(cancel, 0);

        var registration = cancellationToken.Register(() => Marshal.WriteInt32(cancel, 1));

        Exception? failure = null;

        ReplayBridge.InstructionStepCallback onSteps = (steps, stepCount, changes, changeCount, code, codeCount, bytes, byteCount) =>
        {
            if (failure is not null)
            {
                return;
            }

            try
            {
                OnInstructionChunk(hooks, steps, stepCount, changes, changeCount, code, codeCount, bytes, byteCount);
            }
            catch (Exception exception)
            {
                failure = exception;

                Marshal.WriteInt32(cancel, 1);
            }
        };

        ReplayBridge.ProgressCallback onProgress = (_, percent) => progress?.Report(percent);

        var (endSequence, endSteps) = to.IsKnown ? (to.Sequence + 1, 0ul) : (0ul, 0ul);

        try
        {
            var result = ReplayBridge.WatchInstructions(Handle,
                                                        thread,
                                                        from.Sequence,
                                                        from.Steps,
                                                        endSequence,
                                                        endSteps,
                                                        entryAddress,
                                                        0,
                                                        (ulong)Math.Max(0, maximumSteps),
                                                        onSteps,
                                                        onProgress,
                                                        cancel,
                                                        out var outcome);

            GC.KeepAlive(onSteps);

            GC.KeepAlive(onProgress);

            if (failure is not null)
            {
                throw new InvalidOperationException("A trace hook failed while the trace was replayed", failure);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (result != 0)
            {
                throw new InvalidOperationException(Describe(result, TraceFile));
            }

            return (StepReplayOutcome)outcome;
        }
        finally
        {
            registration.Dispose();

            Marshal.FreeHGlobal(cancel);
        }
    }

    private static unsafe void OnInstructionChunk(List<ITraceHook> hooks,
                                                  IntPtr steps,
                                                  int stepCount,
                                                  IntPtr changes,
                                                  int changeCount,
                                                  IntPtr code,
                                                  int codeCount,
                                                  IntPtr bytes,
                                                  int byteCount)
    {
        var stepSpan = new ReadOnlySpan<InstructionStep>((void*)steps, stepCount);

        var changeSpan = new ReadOnlySpan<RegisterChange>((void*)changes, changeCount);

        var codeSpan = new ReadOnlySpan<InstructionCode>((void*)code, codeCount);

        var byteSpan = new ReadOnlySpan<byte>((void*)bytes, byteCount);

        foreach (var hook in hooks)
        {
            hook.OnInstructionChunk(stepSpan, changeSpan, codeSpan, byteSpan);
        }
    }

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

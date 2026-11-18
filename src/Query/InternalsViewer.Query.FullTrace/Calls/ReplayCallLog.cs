using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Calls;

internal sealed class ReplayCallLog
{
    internal Dictionary<(ulong FunctionAddress, ulong InstanceAddress), List<CallEnd>> CallEnds { get; } = new();

    internal Dictionary<(ulong FunctionAddress, ulong InstanceAddress), ChunkedList<FoundLocator>> Locators { get; } = new();

    internal Dictionary<(ulong FunctionAddress, ulong InstanceAddress), StackArgumentList> StackArguments { get; } = new();

    internal LocatorPool LocatorPool { get; } = new();

    private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), List<Task<TraceCallStore.Chunk>>> Pending { get; } = new();

    private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

    private ArrayPool<long> ColumnBuffers { get; } = ArrayPool<long>.Create(1 << 24, Environment.ProcessorCount * 2);

    private ConcurrentBag<MemoryStream> CompressionBuffers { get; } = [];

    public void AddChunk(ulong functionAddress, ulong instanceAddress, IntPtr columns, int callCount)
    {
        var count = callCount * TraceCallColumnSet.ValueCount;

        var buffer = ColumnBuffers.Rent(count);

        Marshal.Copy(columns, buffer, 0, count);

        Compressing.Wait();

        var chunk = Task.Run(() =>
        {
            var compressed = CompressionBuffers.TryTake(out var stream) ? stream : new MemoryStream();

            try
            {
                return TraceCallStore.Compress(MemoryMarshal.Cast<long, ulong>(buffer.AsSpan(0, count)), callCount, 0, compressed);
            }
            finally
            {
                CompressionBuffers.Add(compressed);

                ColumnBuffers.Return(buffer);

                Compressing.Release();
            }
        });

        GetChunks(functionAddress, instanceAddress).Add(chunk);
    }

    public void AddCallEnd(ulong functionAddress,
                           ulong instanceAddress,
                           uint callIndex,
                           ulong toSequence,
                           ulong toSteps,
                           ulong returnValue,
                           uint returned)
    {
        ref var ends = ref CollectionsMarshal.GetValueRefOrAddDefault(CallEnds, (functionAddress, instanceAddress), out _);

        (ends ??= []).Add(new CallEnd(callIndex, new TracePosition(toSequence, toSteps), returnValue, returned != 0));
    }

    public void AddLocator(ulong functionAddress, ulong instanceAddress, int callIndex, Locator locator, bool isReadAtReturn)
    {
        ref var found = ref CollectionsMarshal.GetValueRefOrAddDefault(Locators, (functionAddress, instanceAddress), out _);

        (found ??= []).Add(new FoundLocator(callIndex, LocatorPool.Intern(locator), isReadAtReturn));
    }

    public void AddStackArguments(ulong functionAddress, ulong instanceAddress, uint callIndex, ReadOnlySpan<ulong> values)
    {
        ref var arguments = ref CollectionsMarshal.GetValueRefOrAddDefault(StackArguments, (functionAddress, instanceAddress), out _);

        (arguments ??= new StackArgumentList(values.Length)).Add(callIndex, values);
    }

    internal Dictionary<(ulong FunctionAddress, ulong InstanceAddress), TraceCallStore.Chunk[]> GetChunks()
    {
        Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

        return Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray());
    }

    private List<Task<TraceCallStore.Chunk>> GetChunks(ulong functionAddress, ulong instanceAddress)
    {
        ref var chunks = ref CollectionsMarshal.GetValueRefOrAddDefault(Pending, (functionAddress, instanceAddress), out _);

        return chunks ??= [];
    }

    internal readonly record struct CallEnd(uint CallIndex, TracePosition ToPosition, ulong ReturnValue, bool Returned);

    internal sealed class StackArgumentList(int slotCount)
    {
        public int SlotCount { get; } = slotCount;

        public int Count => CallIndexes.Count;

        public ChunkedList<uint> CallIndexes { get; } = [];

        public ChunkedList<ulong> Values { get; } = [];

        public void Add(uint callIndex, ReadOnlySpan<ulong> values)
        {
            CallIndexes.Add(callIndex);

            foreach (var value in values)
            {
                Values.Add(value);
            }
        }
    }
}

using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    internal sealed class Builder
    {
        private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), List<Task<Chunk>>> Pending { get; } = new();

        private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), ChunkedList<FoundLocator>> Found { get; } = new();

        private LocatorPool Pool { get; } = new();

        private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

        private ArrayPool<long> ColumnBuffers { get; } = ArrayPool<long>.Create(1 << 24, Environment.ProcessorCount * 2);

        private ConcurrentBag<MemoryStream> CompressionBuffers { get; } = [];

        private FoundLocator[] LocatorBuffer { get; set; } = [];

        private int[] IndexBuffer { get; set; } = [];

        private Dictionary<(ulong FunctionAddress, ulong InstanceAddress),
                           List<(uint CallIndex, TracePosition ToPosition, ulong ReturnValue, bool Returned)>>
            ToPositions { get; } = new();

        private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), StackArgumentList> StackArguments { get; } = new();

        public void Add(ulong functionAddress, ulong instanceAddress, IntPtr columns, int calls)
        {
            var count = calls * TraceCallChunk.ValueCount;

            var buffer = ColumnBuffers.Rent(count);

            Marshal.Copy(columns, buffer, 0, count);

            Compressing.Wait();

            var chunk = Task.Run(() =>
            {
                try
                {
                    return Compress(MemoryMarshal.Cast<long, ulong>(buffer.AsSpan(0, count)), calls);
                }
                finally
                {
                    ColumnBuffers.Return(buffer);

                    Compressing.Release();
                }
            });

            GetChunks(functionAddress, instanceAddress).Add(chunk);
        }

        public void Add(ulong functionAddress, ulong instanceAddress, ReadOnlySpan<ulong> columns, int calls)
            => GetChunks(functionAddress, instanceAddress).Add(Task.FromResult(Compress([.. columns], calls)));

        public void AddToPosition(ulong functionAddress,
                                  ulong instanceAddress,
                                  uint callIndex,
                                  ulong toSequence,
                                  ulong toSteps,
                                  ulong returnValue,
                                  uint returned)
        {
            ref var positions = ref CollectionsMarshal.GetValueRefOrAddDefault(ToPositions, (functionAddress, instanceAddress), out _);

            (positions ??= []).Add((callIndex, new TracePosition(toSequence, toSteps), returnValue, returned != 0));
        }

        public void AddLocator(ulong functionAddress, ulong instanceAddress, int callIndex, Locator locator, bool isReadAtReturn)
            => GetFound(functionAddress, instanceAddress).Add(new FoundLocator(callIndex, Pool.Intern(locator), isReadAtReturn));

        public void AddStackArguments(ulong functionAddress, ulong instanceAddress, uint callIndex, ReadOnlySpan<ulong> values)
        {
            ref var arguments = ref CollectionsMarshal.GetValueRefOrAddDefault(StackArguments, (functionAddress, instanceAddress), out _);

            (arguments ??= new StackArgumentList(values.Length)).Add(callIndex, values);
        }

        public TraceCallStore Build()
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            var functions = Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray());

            foreach (var (key, chunks) in functions)
            {
                var positions = ToPositions.GetValueOrDefault(key);

                var stackArguments = StackArguments.GetValueOrDefault(key);

                if (positions is not null || stackArguments is not null)
                {
                    Apply(chunks, positions, stackArguments);
                }
            }

            var packed = Found.ToDictionary(f => f.Key, f => PackLocators(f.Value));

            var used = new bool[Pool.Count];

            foreach (var (_, palette, _) in packed.Values)
            {
                foreach (var poolIndex in palette)
                {
                    used[poolIndex] = true;
                }
            }

            var (uniqueLocators, remap) = Pool.Compact(used);

            var locators = packed.ToDictionary(p => p.Key,
                                               p => new LocatorTable(p.Value.CallIndexes,
                                                                     Remap(p.Value.Palette, remap),
                                                                     p.Value.PaletteIndexes,
                                                                     uniqueLocators));

            return new TraceCallStore(functions, locators, uniqueLocators);
        }

        private static int[] Remap(int[] palette, int[] remap)
        {
            for (var index = 0; index < palette.Length; index++)
            {
                palette[index] = remap[palette[index]];
            }

            return palette;
        }

        private (BitPackedArray CallIndexes, int[] Palette, BitPackedArray PaletteIndexes) PackLocators(ChunkedList<FoundLocator> found)
        {
            if (LocatorBuffer.Length < found.Count)
            {
                LocatorBuffer = new FoundLocator[found.Count];
            }

            if (IndexBuffer.Length < found.Count)
            {
                IndexBuffer = new int[found.Count];
            }

            var span = LocatorBuffer.AsSpan(0, found.Count);

            found.CopyTo(span);

            return LocatorTable.Pack(span, Pool, IndexBuffer.AsSpan(0, found.Count));
        }

        private void Apply(Chunk[] chunks,
                           List<(uint CallIndex, TracePosition ToPosition, ulong ReturnValue, bool Returned)>? positions,
                           StackArgumentList? stackArguments)
        {
            var first = 0;

            var next = 0;

            var slots = stackArguments?.Slots ?? 0;

            ulong[] buffer = [];

            for (var index = 0; index < chunks.Length; index++)
            {
                var calls = chunks[index].Calls;

                var inChunk = positions?.Where(p => p.CallIndex >= first && p.CallIndex < first + calls).ToArray() ?? [];

                if (inChunk.Length == 0 && slots == 0)
                {
                    first += calls;

                    continue;
                }

                var length = calls * (TraceCallChunk.ValueCount + slots);

                if (buffer.Length < length)
                {
                    buffer = new ulong[length];
                }

                var columns = Decompress(chunks[index], ref buffer);

                buffer.AsSpan(calls * TraceCallChunk.ValueCount, calls * slots).Clear();

                if (stackArguments is not null)
                {
                    while (next < stackArguments.Count && stackArguments.Indexes[next] < first + calls)
                    {
                        var callIndex = (int)stackArguments.Indexes[next] - first;

                        for (var slot = 0; callIndex >= 0 && slot < slots; slot++)
                        {
                            buffer[(TraceCallChunk.ValueCount + slot) * calls + callIndex] = stackArguments.Values[next * slots + slot];
                        }

                        next++;
                    }
                }

                foreach (var (callIndex, toPosition, returnValue, returned) in inChunk)
                {
                    columns.SetToPosition((int)callIndex - first, toPosition, returnValue, returned);
                }

                chunks[index] = Compress(buffer.AsSpan(0, length), calls, slots);

                first += calls;
            }
        }

        private Chunk Compress(Span<ulong> columns, int calls, int stackSlots = 0)
        {
            TraceCallChunk.Encode(columns, calls);

            var compressed = CompressionBuffers.TryTake(out var buffer) ? buffer : new MemoryStream();

            try
            {
                compressed.SetLength(0);

                using (var stream = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                {
                    stream.Write(MemoryMarshal.AsBytes(columns));
                }

                return new Chunk(calls, stackSlots, compressed.ToArray());
            }
            finally
            {
                CompressionBuffers.Add(compressed);
            }
        }

        private ChunkedList<FoundLocator> GetFound(ulong functionAddress, ulong instanceAddress)
        {
            ref var found = ref CollectionsMarshal.GetValueRefOrAddDefault(Found, (functionAddress, instanceAddress), out _);

            return found ??= [];
        }

        private List<Task<Chunk>> GetChunks(ulong functionAddress, ulong instanceAddress)
        {
            ref var chunks = ref CollectionsMarshal.GetValueRefOrAddDefault(Pending, (functionAddress, instanceAddress), out _);

            return chunks ??= [];
        }

        private sealed class StackArgumentList(int slots)
        {
            public int Slots { get; } = slots;

            public int Count => Indexes.Count;

            public ChunkedList<uint> Indexes { get; } = [];

            public ChunkedList<ulong> Values { get; } = [];

            public void Add(uint callIndex, ReadOnlySpan<ulong> values)
            {
                Indexes.Add(callIndex);

                foreach (var value in values)
                {
                    Values.Add(value);
                }
            }
        }
    }
}

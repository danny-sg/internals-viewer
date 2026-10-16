using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    internal sealed class Builder
    {
        private Dictionary<(ulong Address, ulong Instance), List<Task<Chunk>>> Pending { get; } = new();

        private Dictionary<(ulong Address, ulong Instance), List<(int Call, Locator Locator, bool IsReadAtReturn)>> Found { get; }
            = new();

        private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

        private Dictionary<(ulong Address, ulong Instance), List<(uint Call, TracePosition ToPosition, ulong ReturnValue, bool Returned)>>
            ToPositions { get; } = new();

        public void Add(ulong address, ulong instance, IntPtr columns, int calls)
        {
            var count = calls * TraceCallColumns.ValueCount;

            var buffer = ArrayPool<long>.Shared.Rent(count);

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
                    ArrayPool<long>.Shared.Return(buffer);

                    Compressing.Release();
                }
            });

            GetChunks(address, instance).Add(chunk);
        }

        public void Add(ulong address, ulong instance, ReadOnlySpan<ulong> columns, int calls)
            => GetChunks(address, instance).Add(Task.FromResult(Compress(columns, calls)));

        public void AddToPosition(ulong address,
                                  ulong instance,
                                  uint call,
                                  ulong toSequence,
                                  ulong toSteps,
                                  ulong returnValue,
                                  uint returned)
        {
            ref var positions = ref CollectionsMarshal.GetValueRefOrAddDefault(ToPositions, (address, instance), out _);

            (positions ??= []).Add((call, new TracePosition(toSequence, toSteps), returnValue, returned != 0));
        }

        public void AddLocator(ulong address, ulong instance, int call, Locator locator, bool isReadAtReturn)
            => GetFound(address, instance).Add((call, locator, isReadAtReturn));

        public TraceCallStore Build()
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            var functions = Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray());

            foreach (var (key, positions) in ToPositions)
            {
                if (functions.TryGetValue(key, out var chunks))
                {
                    ApplyToPositions(chunks, positions);
                }
            }

            return new TraceCallStore(functions, Found.ToDictionary(f => f.Key, f => LocatorTable.Build(GetPreferred(f.Value))));
        }

        private static void ApplyToPositions(Chunk[] chunks,
                                             List<(uint Call, TracePosition ToPosition, ulong ReturnValue, bool Returned)> positions)
        {
            var first = 0;

            for (var index = 0; index < chunks.Length; index++)
            {
                var calls = chunks[index].Calls;

                var inChunk = positions.Where(p => p.Call >= first && p.Call < first + calls).ToArray();

                if (inChunk.Length > 0)
                {
                    var columns = Decompress(chunks[index]);

                    foreach (var (call, toPosition, returnValue, returned) in inChunk)
                    {
                        columns.SetToPosition((int)call - first, toPosition, returnValue, returned);
                    }

                    chunks[index] = Compress(columns.Values, calls);
                }

                first += calls;
            }
        }

        private static List<(int Call, Locator Locator)> GetPreferred(List<(int Call, Locator Locator, bool IsReadAtReturn)> found)
        {
            var returned = found.Where(f => f.IsReadAtReturn).Select(f => (f.Call, f.Locator.Register, f.Locator.ValueType)).ToHashSet();

            return [.. found.Where(f => f.IsReadAtReturn || !returned.Contains((f.Call, f.Locator.Register, f.Locator.ValueType)))
                            .Select(f => (f.Call, f.Locator))];
        }

        private static Chunk Compress(ReadOnlySpan<ulong> columns, int calls)
        {
            using var compressed = new MemoryStream();

            using (var stream = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                stream.Write(MemoryMarshal.AsBytes(columns));
            }

            return new Chunk(calls, compressed.ToArray());
        }

        private List<(int Call, Locator Locator, bool IsReadAtReturn)> GetFound(ulong address, ulong instance)
        {
            ref var found = ref CollectionsMarshal.GetValueRefOrAddDefault(Found, (address, instance), out _);

            return found ??= [];
        }

        private List<Task<Chunk>> GetChunks(ulong address, ulong instance)
        {
            ref var chunks = ref CollectionsMarshal.GetValueRefOrAddDefault(Pending, (address, instance), out _);

            return chunks ??= [];
        }
    }
}

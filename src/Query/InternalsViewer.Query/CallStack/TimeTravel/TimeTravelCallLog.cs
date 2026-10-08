using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed class TimeTravelCallLog
{
    private const int IntegerColumn = 2;

    private const int EntryPointeeColumn = 14;

    private const int ReturnPointeeColumn = 22;

    private const int ReturnColumn = 30;

    private const int FlagsColumn = 1;

    private const int Slots = 8;

    private const int StackSlot = 4;

    private const ulong ReturnedFlag = 1ul << 32;

    private const ulong StackReadFlag = 1ul << 33;

    private const int EntryPointeeShift = 40;

    private const int ReturnPointeeShift = 48;

    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

    private TimeTravelCallLog(Dictionary<(ulong Address, ulong Instance), Chunk[]> functions)
    {
        Functions = functions;
        Calls = functions.Values.Sum(f => f.Sum(c => (long)c.Calls));
        Size = functions.Values.Sum(f => f.Sum(c => (long)c.Columns.Length));
    }

    public int FunctionCount => Functions.Count;

    public long Calls { get; }

    public long Size { get; }

    private Dictionary<(ulong Address, ulong Instance), Chunk[]> Functions { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    public TimeTravelCallList? CallsOf(ulong address, ulong instance)
        => Functions.TryGetValue((address, instance), out var chunks) ? new TimeTravelCallList(address, instance, chunks) : null;

    public CallStackNode? NodeOf(TimeTravelArgumentCall call)
        => call.Node >= 0 && call.Node < Nodes.Length ? Nodes[call.Node] : null;

    public Task<IReadOnlyList<TimeTravelValueUse>> FindAsync(ulong value, CancellationToken cancellationToken)
        => Task.Run<IReadOnlyList<TimeTravelValueUse>>(() => Find(value, cancellationToken), cancellationToken);

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal static ulong[] Decompress(Chunk chunk)
    {
        var columns = new ulong[chunk.Calls * TimeTravelArgumentCall.ValueCount];

        using var stream = new ZLibStream(new MemoryStream(chunk.Columns), CompressionMode.Decompress);

        stream.ReadExactly(MemoryMarshal.AsBytes(columns.AsSpan()));

        return columns;
    }

    private List<TimeTravelValueUse> Find(ulong value, CancellationToken cancellationToken)
    {
        var uses = new Dictionary<(ulong Address, ulong Instance, string Location), TimeTravelValueUse>();

        var sync = new Lock();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.ForEach(Scans(), options, scan =>
        {
            var (address, instance, start, chunk) = scan;

            var columns = Decompress(chunk);

            var found = new Dictionary<string, (int Calls, int First, int Last)>();

            for (var call = 0; call < chunk.Calls; call++)
            {
                foreach (var location in Locations(columns, chunk.Calls, call, value))
                {
                    found[location] = found.TryGetValue(location, out var counted)
                        ? (counted.Calls + 1, counted.First, call)
                        : (1, call, call);
                }
            }

            if (found.Count == 0)
            {
                return;
            }

            lock (sync)
            {
                foreach (var (location, (calls, first, last)) in found)
                {
                    var use = new TimeTravelValueUse(address,
                                                     instance,
                                                     location,
                                                     calls,
                                                     MatchAt(columns, chunk.Calls, first, start),
                                                     MatchAt(columns, chunk.Calls, last, start));

                    uses[(address, instance, location)] = uses.TryGetValue((address, instance, location), out var existing)
                        ? Merge(existing, use)
                        : use;
                }
            }
        });

        return [.. uses.Values.OrderBy(u => u.First.Sequence)];
    }

    private TimeTravelValueMatch MatchAt(ulong[] columns, int calls, int call, int start)
    {
        var node = (int)columns[(TimeTravelArgumentCall.ValueCount - 1) * calls + call];

        return new TimeTravelValueMatch(start + call, columns[call], node >= 0 && node < Nodes.Length ? Nodes[node] : null);
    }

    private static TimeTravelValueUse Merge(TimeTravelValueUse existing, TimeTravelValueUse use) => existing with
    {
        Calls = existing.Calls + use.Calls,
        First = use.First.Sequence < existing.First.Sequence ? use.First : existing.First,
        Last = use.Last.Sequence > existing.Last.Sequence ? use.Last : existing.Last
    };

    private IEnumerable<(ulong Address, ulong Instance, int Start, Chunk Chunk)> Scans()
    {
        foreach (var ((address, instance), chunks) in Functions)
        {
            var start = 0;

            foreach (var chunk in chunks)
            {
                yield return (address, instance, start, chunk);

                start += chunk.Calls;
            }
        }
    }

    private static IEnumerable<string> Locations(ulong[] columns, int calls, int call, ulong value)
    {
        var flags = columns[FlagsColumn * calls + call];

        for (var slot = 0; slot < Slots; slot++)
        {
            var readable = slot < StackSlot || (flags & StackReadFlag) != 0;

            if (readable && columns[(IntegerColumn + slot) * calls + call] == value)
            {
                yield return Slot(slot);
            }

            if ((flags & (1ul << (EntryPointeeShift + slot))) != 0 && columns[(EntryPointeeColumn + slot) * calls + call] == value)
            {
                yield return $"*{Slot(slot)} On Entry";
            }

            if ((flags & (1ul << (ReturnPointeeShift + slot))) != 0 && columns[(ReturnPointeeColumn + slot) * calls + call] == value)
            {
                yield return $"*{Slot(slot)} On Return";
            }
        }

        if ((flags & ReturnedFlag) != 0 && columns[ReturnColumn * calls + call] == value)
        {
            yield return "RAX";
        }
    }

    private static string Slot(int slot)
        => slot < StackSlot ? Registers[slot] : $"[RSP+0x{0x28 + (slot - StackSlot) * sizeof(ulong):X}]";

    internal sealed record Chunk(int Calls, byte[] Columns);

    internal sealed class Builder
    {
        private Dictionary<(ulong Address, ulong Instance), List<Task<Chunk>>> Pending { get; } = new();

        private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

        public void Add(ulong address, ulong instance, IntPtr columns, int calls)
        {
            var count = calls * TimeTravelArgumentCall.ValueCount;

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

            ChunksOf(address, instance).Add(chunk);
        }

        public void Add(ulong address, ulong instance, ReadOnlySpan<ulong> columns, int calls)
            => ChunksOf(address, instance).Add(Task.FromResult(Compress(columns, calls)));

        public TimeTravelCallLog Build()
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            return new TimeTravelCallLog(Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray()));
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

        private List<Task<Chunk>> ChunksOf(ulong address, ulong instance)
        {
            if (!Pending.TryGetValue((address, instance), out var chunks))
            {
                chunks = [];

                Pending[(address, instance)] = chunks;
            }

            return chunks;
        }
    }
}

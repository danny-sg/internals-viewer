using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel.CallLog;

public sealed class TimeTravelCallLog
{
    private const int IntegerColumn = 2;

    private const int ReturnColumn = 6;

    private const int FlagsColumn = 1;

    private const ulong ReturnedFlag = 1ul << 32;

    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

    private TimeTravelCallLog(Dictionary<(ulong Address, ulong Instance), Chunk[]> functions,
                              Dictionary<(ulong Address, ulong Instance), TimeTravelLocatorTable> locators)
    {
        Functions = functions;
        Locators = locators;
        Calls = functions.Values.Sum(f => f.Sum(c => (long)c.Calls));
        Size = functions.Values.Sum(f => f.Sum(c => (long)c.Columns.Length));
        LocatorCount = locators.Values.Sum(l => (long)l.Count);
    }

    public int FunctionCount => Functions.Count;

    public long Calls { get; }

    public long Size { get; }

    public long LocatorCount { get; }

    private Dictionary<(ulong Address, ulong Instance), Chunk[]> Functions { get; }

    private Dictionary<(ulong Address, ulong Instance), TimeTravelLocatorTable> Locators { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    public TimeTravelCallList? CallsOf(ulong address, ulong instance)
        => Functions.TryGetValue((address, instance), out var chunks)
            ? new TimeTravelCallList(address, instance, chunks, Locators.GetValueOrDefault((address, instance)))
            : null;

    public CallStackNode? NodeOf(TimeTravelArgumentCall call) => NodeAt(call.Node);

    public Task<IReadOnlyList<TimeTravelValueUse>> FindAsync(ulong value, CancellationToken cancellationToken)
        => Task.Run<IReadOnlyList<TimeTravelValueUse>>(() => Find(value, cancellationToken), cancellationToken);

    internal static TimeTravelCallLog Read(BinaryReader reader)
    {
        var functions = new Dictionary<(ulong Address, ulong Instance), Chunk[]>();

        var functionCount = reader.ReadInt32();

        for (var index = 0; index < functionCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            var chunks = new Chunk[reader.ReadInt32()];

            for (var chunk = 0; chunk < chunks.Length; chunk++)
            {
                var calls = reader.ReadInt32();

                chunks[chunk] = new Chunk(calls, ExportBinary.ReadArray<byte>(reader));
            }

            functions[key] = chunks;
        }

        var locators = new Dictionary<(ulong Address, ulong Instance), TimeTravelLocatorTable>();

        var tableCount = reader.ReadInt32();

        for (var index = 0; index < tableCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            locators[key] = TimeTravelLocatorTable.Read(reader);
        }

        return new TimeTravelCallLog(functions, locators);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Functions.Count);

        foreach (var ((address, instance), chunks) in Functions)
        {
            writer.Write(address);

            writer.Write(instance);

            writer.Write(chunks.Length);

            foreach (var chunk in chunks)
            {
                writer.Write(chunk.Calls);

                ExportBinary.WriteArray(writer, chunk.Columns);
            }
        }

        writer.Write(Locators.Count);

        foreach (var ((address, instance), table) in Locators)
        {
            writer.Write(address);

            writer.Write(instance);

            table.Write(writer);
        }
    }

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal void AnnotateNodes()
    {
        foreach (var ((address, instance), table) in Locators)
        {
            if (CallsOf(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var (call, locator) in table.Entries())
            {
                if (locator.Kind == LocatorKind.Referenced)
                {
                    continue;
                }

                if (call < calls.Count && NodeAt(calls.NodeIndexAt(call)) is { } node)
                {
                    (node.Locators ??= new TimeTravelLocatorSet()).Add(locator);
                }
            }
        }
    }

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

    private CallStackNode? NodeAt(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

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

        for (var slot = 0; slot < TimeTravelArgumentCall.RegisterCount; slot++)
        {
            if (columns[(IntegerColumn + slot) * calls + call] == value)
            {
                yield return Registers[slot];
            }
        }

        if ((flags & ReturnedFlag) != 0 && columns[ReturnColumn * calls + call] == value)
        {
            yield return "RAX";
        }
    }

    internal sealed record Chunk(int Calls, byte[] Columns);

    internal sealed class Builder(IEnumerable<LocatorRead>? locatorReads = null)
    {
        private Dictionary<(ulong Address, ulong Instance), List<Task<Chunk>>> Pending { get; } = new();

        private Dictionary<(ulong Function, int Register, int Offset, bool AtReturn), LocatorRead> Reads { get; }
            = (locatorReads ?? []).DistinctBy(r => (r.Function, r.Register, r.Offset, r.AtReturn))
                                  .ToDictionary(r => (r.Function, r.Register, r.Offset, r.AtReturn));

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register), ulong[]> ObjectKeys { get; } = new();

        private Dictionary<(ulong Address, ulong Instance), List<(int Call, TimeTravelLocator Locator, bool AtReturn)>> Found { get; }
            = new();

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

        public void AddLocators(IntPtr values, int count)
        {
            var size = count * Marshal.SizeOf<TimeTravelMemoryValue>();

            var buffer = ArrayPool<byte>.Shared.Rent(size);

            try
            {
                Marshal.Copy(values, buffer, 0, size);

                AddLocators(MemoryMarshal.Cast<byte, TimeTravelMemoryValue>(buffer.AsSpan(0, size)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void AddLocators(ReadOnlySpan<TimeTravelMemoryValue> values)
        {
            foreach (var value in values)
            {
                var atReturn = value.AtReturn != 0;

                if (!Reads.TryGetValue((value.Function, value.Register, (int)value.Offset, atReturn), out var read))
                {
                    continue;
                }

                if (read.Kind != LocatorKind.ColumnStoreObject)
                {
                    FoundFor(value).Add(((int)value.Call, new TimeTravelLocator(read.Register, read.Kind, value.Value), atReturn));

                    continue;
                }

                if (AddKeyPart(value) is { } key)
                {
                    FoundFor(value).Add(((int)value.Call,
                                         TimeTravelLocator.ColumnStoreObject(read.Register, key[0], key[1], key[2]),
                                         atReturn));
                }
            }
        }

        public TimeTravelCallLog Build()
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            return new TimeTravelCallLog(Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray()),
                                         Found.ToDictionary(f => f.Key, f => TimeTravelLocatorTable.Build(Preferred(f.Value))));
        }

        private static List<(int Call, TimeTravelLocator Locator)> Preferred(
            List<(int Call, TimeTravelLocator Locator, bool AtReturn)> found)
        {
            var returned = found.Where(f => f.AtReturn).Select(f => (f.Call, f.Locator.Register)).ToHashSet();

            return [.. found.Where(f => f.AtReturn || !returned.Contains((f.Call, f.Locator.Register))).Select(f => (f.Call, f.Locator))];
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

        private List<(int Call, TimeTravelLocator Locator, bool AtReturn)> FoundFor(TimeTravelMemoryValue value)
        {
            if (!Found.TryGetValue((value.Function, value.Instance), out var found))
            {
                found = [];

                Found[(value.Function, value.Instance)] = found;
            }

            return found;
        }

        private ulong[]? AddKeyPart(TimeTravelMemoryValue value)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register);

            if (!ObjectKeys.TryGetValue(identity, out var key))
            {
                key = new ulong[LocatorReadResolver.ObjectKeyParts + 1];

                ObjectKeys[identity] = key;
            }

            var part = (int)value.Offset / sizeof(ulong);

            key[part] = value.Value;

            key[LocatorReadResolver.ObjectKeyParts] |= 1ul << part;

            if (key[LocatorReadResolver.ObjectKeyParts] != (1ul << LocatorReadResolver.ObjectKeyParts) - 1)
            {
                return null;
            }

            ObjectKeys.Remove(identity);

            return key;
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

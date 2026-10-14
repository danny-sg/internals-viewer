using InternalsViewer.Query.CallStack;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed partial class TraceCallLog
{
    private const int IntegerColumn = 2;

    private const int ReturnColumn = 6;

    private const int FlagsColumn = 1;

    private const int NodeColumn = 7;

    private const ulong ReturnedFlag = 1ul << 32;

    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

    private TraceCallLog(Dictionary<(ulong Address, ulong Instance), Chunk[]> functions,
                         Dictionary<(ulong Address, ulong Instance), LocatorTable> locators)
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

    private Dictionary<(ulong Address, ulong Instance), LocatorTable> Locators { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    public TraceCallList? CallsOf(ulong address, ulong instance)
        => Functions.TryGetValue((address, instance), out var chunks)
            ? new TraceCallList(address, instance, chunks, Locators.GetValueOrDefault((address, instance)))
            : null;

    public CallStackNode? NodeOf(TraceArgumentCall call) => NodeAt(call.Node);

    public CallStackNode? NodeAt(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

    public Task<IReadOnlyList<TraceValueUse>> FindAsync(ulong value,
                                                        CancellationToken cancellationToken,
                                                        Func<CallStackNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueUse>>(() => Find(value, include, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<TraceValueMatch>> FindCallsAsync(TraceValueUse use,
                                                               ulong value,
                                                               CancellationToken cancellationToken,
                                                               Func<CallStackNode?, bool>? include = null)
        => Task.Run<IReadOnlyList<TraceValueMatch>>(() => FindCalls(use, value, include, cancellationToken), cancellationToken);

    public IReadOnlyList<int> IncludedCalls(TraceCallList calls, Func<CallStackNode?, bool> include, CancellationToken cancellationToken)
    {
        var included = new List<int>(calls.Count);

        for (var index = 0; index < calls.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (include(NodeAt(calls.NodeIndexAt(index))))
            {
                included.Add(index);
            }
        }

        return included;
    }

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal IEnumerable<(ulong Address, ulong Instance)> FunctionKeys => Functions.Keys;

    internal IEnumerable<(ulong Sequence, int Node, ulong Self)> GetSelves(ulong address, ulong instance)
    {
        if (!Functions.TryGetValue((address, instance), out var chunks))
        {
            yield break;
        }

        foreach (var chunk in chunks)
        {
            var columns = Decompress(chunk);

            for (var call = 0; call < chunk.Calls; call++)
            {
                yield return (columns[call], (int)columns[NodeColumn * chunk.Calls + call], columns[IntegerColumn * chunk.Calls + call]);
            }
        }
    }

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
                if (locator.Kind is not (LocatorKind.Page or LocatorKind.Row or LocatorKind.ColumnStoreObject))
                {
                    continue;
                }

                if (call < calls.Count && NodeAt(calls.NodeIndexAt(call)) is { } node)
                {
                    (node.Locators ??= new LocatorSet()).Add(locator);
                }
            }
        }
    }

    internal static ulong[] Decompress(Chunk chunk)
    {
        var columns = new ulong[chunk.Calls * TraceArgumentCall.ValueCount];

        using var stream = new ZLibStream(new MemoryStream(chunk.Columns), CompressionMode.Decompress);

        var bytes = MemoryMarshal.AsBytes(columns.AsSpan());

        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);

        bytes[read..].Clear();

        return columns;
    }

    private List<TraceValueUse> Find(ulong value, Func<CallStackNode?, bool>? include, CancellationToken cancellationToken)
    {
        var uses = new Dictionary<(ulong Address, ulong Instance, string Location), TraceValueUse>();

        var sync = new Lock();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.ForEach(Scans(), options, scan =>
        {
            var (address, instance, start, chunk) = scan;

            var columns = Decompress(chunk);

            var found = new Dictionary<string, (int Calls, int First, int Last)>();

            for (var call = 0; call < chunk.Calls; call++)
            {
                if (!IsIncluded(columns, chunk.Calls, call, include))
                {
                    continue;
                }

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
                    var use = new TraceValueUse(address,
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

        AddStackUses(uses, value, include, cancellationToken);

        return [.. uses.Values.OrderBy(u => u.First.Sequence)];
    }

    private List<TraceValueMatch> FindCalls(TraceValueUse use,
                                            ulong value,
                                            Func<CallStackNode?, bool>? include,
                                            CancellationToken cancellationToken)
    {
        if (GetStackPosition(use.Location) is { } position)
        {
            return FindStackCalls(use, value, position, include, cancellationToken);
        }

        var matches = new List<TraceValueMatch>();

        if (!Functions.TryGetValue((use.Address, use.Instance), out var chunks))
        {
            return matches;
        }

        var start = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = Decompress(chunk);

            for (var call = 0; call < chunk.Calls; call++)
            {
                if (IsIncluded(columns, chunk.Calls, call, include) && Locations(columns, chunk.Calls, call, value).Contains(use.Location))
                {
                    matches.Add(MatchAt(columns, chunk.Calls, call, start));
                }
            }

            start += chunk.Calls;
        }

        return matches;
    }

    private static string GetStackLocation(int position) => $"[RSP+0x{ArgumentLayout.StackOffset(position):X}]";

    private static int? GetStackPosition(string location)
    {
        for (var position = TraceArgumentCall.RegisterCount; position < ArgumentLayout.CapturedSlots; position++)
        {
            if (location == GetStackLocation(position))
            {
                return position;
            }
        }

        return null;
    }

    private void AddStackUses(Dictionary<(ulong Address, ulong Instance, string Location), TraceValueUse> uses,
                              ulong value,
                              Func<CallStackNode?, bool>? include,
                              CancellationToken cancellationToken)
    {
        foreach (var ((address, instance), table) in Locators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TraceCallList? calls = null;

            foreach (var (call, locator) in table.Entries())
            {
                if (locator.Kind != LocatorKind.Argument || locator.Value != value)
                {
                    continue;
                }

                calls ??= CallsOf(address, instance);

                if (GetStackMatch(calls, call, include) is not { } match)
                {
                    continue;
                }

                var location = GetStackLocation(locator.Register);

                var use = new TraceValueUse(address, instance, location, 1, match, match);

                uses[(address, instance, location)] = uses.TryGetValue((address, instance, location), out var existing)
                    ? Merge(existing, use)
                    : use;
            }
        }
    }

    private List<TraceValueMatch> FindStackCalls(TraceValueUse use,
                                                 ulong value,
                                                 int position,
                                                 Func<CallStackNode?, bool>? include,
                                                 CancellationToken cancellationToken)
    {
        var matches = new List<TraceValueMatch>();

        if (!Locators.TryGetValue((use.Address, use.Instance), out var table) || CallsOf(use.Address, use.Instance) is not { } calls)
        {
            return matches;
        }

        foreach (var (call, locator) in table.Entries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (locator.Kind == LocatorKind.Argument
                && locator.Register == position
                && locator.Value == value
                && GetStackMatch(calls, call, include) is { } match)
            {
                matches.Add(match);
            }
        }

        return matches;
    }

    private TraceValueMatch? GetStackMatch(TraceCallList? calls, int call, Func<CallStackNode?, bool>? include)
    {
        if (calls is null || call >= calls.Count)
        {
            return null;
        }

        var argument = calls[call];

        var node = NodeAt(argument.Node);

        return include is null || include(node) ? new TraceValueMatch(call, argument.Sequence, node, argument.ThreadId) : null;
    }

    private bool IsIncluded(ulong[] columns, int calls, int call, Func<CallStackNode?, bool>? include)
        => include is null || include(NodeAt((int)columns[TraceArgumentCall.NodeIndex * calls + call]));

    private TraceValueMatch MatchAt(ulong[] columns, int calls, int call, int start)
    {
        var node = (int)columns[TraceArgumentCall.NodeIndex * calls + call];

        var thread = (uint)columns[FlagsColumn * calls + call];

        return new TraceValueMatch(start + call, columns[call], node >= 0 && node < Nodes.Length ? Nodes[node] : null, thread);
    }

    private static TraceValueUse Merge(TraceValueUse existing, TraceValueUse use) => existing with
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

        for (var slot = 0; slot < TraceArgumentCall.RegisterCount; slot++)
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

        private Dictionary<(ulong Function, int Register, int Offset, bool IsReadAtReturn, uint Mode), LocatorRead> Reads { get; }
            = (locatorReads ?? []).DistinctBy(KeyOf).ToDictionary(KeyOf);

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), ulong[]> ObjectKeys { get; }
            = new();

        private Dictionary<(ulong Address, ulong Instance), List<(int Call, Locator Locator, bool IsReadAtReturn)>> Found { get; }
            = new();

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn),
                           (LocatorKind Kind, SortedList<uint, ulong> Parts)> TextParts { get; } = new();

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn),
                           (ulong Header, ulong Details, bool HasHeader)> TypeInfoParts { get; } = new();

        private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

        public void Add(ulong address, ulong instance, IntPtr columns, int calls)
        {
            var count = calls * TraceArgumentCall.ValueCount;

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
            var size = count * Marshal.SizeOf<PointerValue>();

            var buffer = ArrayPool<byte>.Shared.Rent(size);

            try
            {
                Marshal.Copy(values, buffer, 0, size);

                AddLocators(MemoryMarshal.Cast<byte, PointerValue>(buffer.AsSpan(0, size)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void AddLocators(ReadOnlySpan<PointerValue> values)
        {
            foreach (var value in values)
            {
                var isReadAtReturn = value.IsReadAtReturn != 0;

                if (!Reads.TryGetValue((value.Function, value.Register, (int)value.Offset, isReadAtReturn, value.Mode), out var read))
                {
                    continue;
                }

                if (read.Kind is LocatorKind.Text or LocatorKind.WideText)
                {
                    AddTextPart(value, read.Kind);

                    continue;
                }

                if (read.Kind is LocatorKind.TypeInfo)
                {
                    AddTypeInfoPart(value);

                    continue;
                }

                var parts = LocatorReadResolver.GetParts(read.Kind);

                if (parts == 1)
                {
                    FoundFor(value).Add(((int)value.Call, new Locator(read.Register, read.Kind, value.Value), isReadAtReturn));

                    continue;
                }

                if (AddKeyPart(value, parts) is not { } key)
                {
                    continue;
                }

                var locator = read.Kind == LocatorKind.LogSequenceNumber
                    ? Locator.CreateLogSequenceNumber(read.Register, key[0], key[1])
                    : Locator.ColumnStoreObject(read.Register, key[0], key[1], key[2]);

                FoundFor(value).Add(((int)value.Call, locator, isReadAtReturn));
            }
        }

        public TraceCallLog Build(ImageText? images = null)
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            var texts = new Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), Locator>();

            foreach (var (identity, (kind, parts)) in TextParts)
            {
                var text = TextDecoder.Decode(parts, kind == LocatorKind.WideText, out var isTruncated);

                texts[identity] = new Locator(identity.Register, kind, (ulong)text.Length, isTruncated ? 1ul : 0, text);
            }

            if (images is not null)
            {
                AddImageTexts(images, texts);
            }

            foreach (var (identity, (header, details, hasHeader)) in TypeInfoParts)
            {
                if (hasHeader)
                {
                    texts[identity] = new Locator(identity.Register, LocatorKind.TypeInfo, header, details);
                }
            }

            TypeInfoParts.Clear();

            foreach (var ((function, instance, call, _, isReadAtReturn), locator) in texts)
            {
                FoundFor(function, instance).Add(((int)call, locator, isReadAtReturn != 0));
            }

            TextParts.Clear();

            return new TraceCallLog(Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray()),
                                    Found.ToDictionary(f => f.Key, f => LocatorTable.Build(Preferred(f.Value))));
        }

        private void AddImageTexts(ImageText images,
                                   Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), Locator> texts)
        {
            var functions = Pending.Keys.ToLookup(k => k.Address);

            foreach (var read in Reads.Values.Where(IsImageText))
            {
                foreach (var (address, instance) in functions[read.Function])
                {
                    var stackValues = read.Register is >= TraceArgumentCall.RegisterCount and not LocatorReadResolver.ReturnRegister
                        ? GetStackValues(address, instance, read.Register)
                        : null;

                    var call = 0u;

                    foreach (var chunk in Pending[(address, instance)].Select(c => c.Result))
                    {
                        var columns = Decompress(chunk);

                        for (var index = 0; index < chunk.Calls; index++, call++)
                        {
                            var identity = (address, instance, call, read.Register, read.IsReadAtReturn ? 1u : 0u);

                            if (texts.TryGetValue(identity, out var existing) && existing.Detail == 0)
                            {
                                continue;
                            }

                            var pointer = GetPointer(read, columns, chunk.Calls, index, stackValues, call);

                            if (pointer is not 0
                                && images.Read(pointer + (ulong)read.Offset, read.Kind == LocatorKind.WideText) is { } text)
                            {
                                texts[identity] = new Locator(read.Register, read.Kind, (ulong)text.Length, 0, text);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsImageText(LocatorRead read)
            => read.Kind is LocatorKind.Text or LocatorKind.WideText && read.Path is not { Length: > 0 };

        private static ulong GetPointer(LocatorRead read,
                                        ulong[] columns,
                                        int calls,
                                        int index,
                                        Dictionary<uint, ulong>? stackValues,
                                        uint call)
        {
            if (read.Register == LocatorReadResolver.ReturnRegister)
            {
                return (columns[FlagsColumn * calls + index] & ReturnedFlag) != 0 ? columns[ReturnColumn * calls + index] : 0;
            }

            if (read.Register < TraceArgumentCall.RegisterCount)
            {
                return columns[(IntegerColumn + read.Register) * calls + index];
            }

            return stackValues?.GetValueOrDefault(call) ?? 0;
        }

        private Dictionary<uint, ulong> GetStackValues(ulong address, ulong instance, int register)
            => FoundFor(address, instance).Where(f => f.Locator is { Kind: LocatorKind.Argument } && f.Locator.Register == register)
                                          .GroupBy(f => (uint)f.Call)
                                          .ToDictionary(g => g.Key, g => g.First().Locator.Value);

        private static List<(int Call, Locator Locator)> Preferred(
            List<(int Call, Locator Locator, bool IsReadAtReturn)> found)
        {
            var returned = found.Where(f => f.IsReadAtReturn).Select(f => (f.Call, f.Locator.Register, f.Locator.Kind)).ToHashSet();

            return [.. found.Where(f => f.IsReadAtReturn || !returned.Contains((f.Call, f.Locator.Register, f.Locator.Kind)))
                            .Select(f => (f.Call, f.Locator))];
        }

        private static (ulong Function, int Register, int Offset, bool IsReadAtReturn, uint Mode) KeyOf(LocatorRead read)
            => (read.Function, read.Register, read.Offset, read.IsReadAtReturn, PointerRead.ModeOf(read.Kind));

        private static Chunk Compress(ReadOnlySpan<ulong> columns, int calls)
        {
            using var compressed = new MemoryStream();

            using (var stream = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                stream.Write(MemoryMarshal.AsBytes(columns));
            }

            return new Chunk(calls, compressed.ToArray());
        }

        private List<(int Call, Locator Locator, bool IsReadAtReturn)> FoundFor(PointerValue value)
            => FoundFor(value.Function, value.Instance);

        private List<(int Call, Locator Locator, bool IsReadAtReturn)> FoundFor(ulong function, ulong instance)
        {
            if (!Found.TryGetValue((function, instance), out var found))
            {
                found = [];

                Found[(function, instance)] = found;
            }

            return found;
        }

        private void AddTextPart(PointerValue value, LocatorKind kind)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            if (!TextParts.TryGetValue(identity, out var text))
            {
                text = (kind, new SortedList<uint, ulong>());

                TextParts[identity] = text;
            }

            text.Parts[value.Part] = value.Value;
        }

        private void AddTypeInfoPart(PointerValue value)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            var parts = TypeInfoParts.GetValueOrDefault(identity);

            TypeInfoParts[identity] = value.Offset < LocatorReadResolver.TypeInfoDetailOffset
                ? parts with { Header = value.Value, HasHeader = true }
                : parts with { Details = value.Value };
        }

        private ulong[]? AddKeyPart(PointerValue value, int parts)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            if (!ObjectKeys.TryGetValue(identity, out var key))
            {
                key = new ulong[parts + 1];

                ObjectKeys[identity] = key;
            }

            var part = (int)value.Offset / sizeof(ulong);

            if (part >= parts)
            {
                return null;
            }

            key[part] = value.Value;

            key[parts] |= 1ul << part;

            if (key[parts] != (1ul << parts) - 1)
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

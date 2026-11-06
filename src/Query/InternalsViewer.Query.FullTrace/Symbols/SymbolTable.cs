using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.Events.Parsing;
using InternalsViewer.Query.FullTrace.Symbols.Dia;

namespace InternalsViewer.Query.FullTrace.Symbols;

internal sealed class SymbolTable
{
    private const int ChunkShift = 7;

    private const int ChunkSize = 1 << ChunkShift;

    private const int ChunkMask = ChunkSize - 1;

    private const int FieldCount = 3;

    private const int NameField = 0;

    private const int SignatureField = 1;

    private const int DecoratedField = 2;

    private const int CacheSlots = 16;

    private const ushort FunctionFlag = 1;

    private const ushort ConstructorFlag = 2;

    private const int TypeShift = 2;

    private const int TypeMask = 3;

    private const ushort FirstFlag = 16;

    private const ushort FoldedNameFlag = 32;

    private const ushort FoldedParametersFlag = 64;

    private const ushort ReturnBufferFlag = 128;

    private const ushort GeneratedFlag = 256;

    private const ushort FoldedClassFlag = 512;

    private const string ConstructorPrefix = "??0";

    private SymbolTable(uint[] rvas,
                        ushort[] flags,
                        short[] separators,
                        short[] parameterCounts,
                        int[] firstParameters,
                        string?[] returnTypes,
                        string[] parameterTypes,
                        int[] classIds,
                        string[] classes,
                        byte[][] chunks,
                        int[] byAddress)
    {
        Rvas = rvas;
        Flags = flags;
        Separators = separators;
        ParameterCounts = parameterCounts;
        FirstParameters = firstParameters;
        ReturnTypes = returnTypes;
        ParameterTypes = parameterTypes;
        ClassIds = classIds;
        Classes = classes;
        Chunks = chunks;
        ByAddress = byAddress;
        Cache = new TextChunk?[CacheSlots];
        FunctionCount = flags.Count(f => (f & FirstFlag) != 0);
    }

    public int Count => Rvas.Length;

    public int FunctionCount { get; }

    private uint[] Rvas { get; }

    private int[] ByAddress { get; }

    private ushort[] Flags { get; }

    private short[] Separators { get; }

    private short[] ParameterCounts { get; }

    private int[] FirstParameters { get; }

    private string?[] ReturnTypes { get; }

    private string[] ParameterTypes { get; }

    private int[] ClassIds { get; }

    private string[] Classes { get; }

    private Dictionary<string, int> ClassLookup
        => field ??= Classes.Select((c, i) => (Class: c, Id: i)).ToDictionary(c => c.Class, c => c.Id, StringComparer.Ordinal);

    private byte[][] Chunks { get; }

    private TextChunk?[] Cache { get; }

    private Lock Sync { get; } = new();

    private Lock IndexSync { get; } = new();

    private WeakReference<SymbolIndex>? SearchIndex { get; set; }

    public Symbol GetSymbol(int index) => new(this, index);

    public uint GetRva(int index) => Rvas[index];

    public bool IsFunction(int index) => (Flags[index] & FunctionFlag) != 0;

    public bool IsConstructor(int index) => (Flags[index] & ConstructorFlag) != 0;

    public bool IsFirst(int index) => (Flags[index] & FirstFlag) != 0;

    public bool HasFoldedName(int index) => (Flags[index] & FoldedNameFlag) != 0;

    public bool HasFoldedParameters(int index) => (Flags[index] & FoldedParametersFlag) != 0;

    public bool HasFoldedClass(int index) => (Flags[index] & FoldedClassFlag) != 0;

    public bool IsGenerated(int index) => (Flags[index] & GeneratedFlag) != 0;

    public FunctionType? GetFunctionType(int index)
        => (Flags[index] >> TypeShift & TypeMask) is var type and > 0 ? (FunctionType)(type - 1) : null;

    public int? GetParameterCount(int index) => ParameterCounts[index] < 0 ? null : ParameterCounts[index];

    public int GetSeparator(int index) => Separators[index];

    public string GetClassName(int index) => ClassIds[index] < 0 ? string.Empty : Classes[ClassIds[index]];

    public string GetName(int index) => GetText(index, NameField);

    public string GetSignature(int index) => GetText(index, SignatureField);

    public FunctionSignature? GetParsedSignature(int index)
    {
        if (ParameterCounts[index] < 0)
        {
            return null;
        }

        return new FunctionSignature(new ArraySegment<string>(ParameterTypes, FirstParameters[index], ParameterCounts[index]),
                                     GetFunctionType(index) ?? FunctionType.Free,
                                     ReturnTypes[index],
                                     (Flags[index] & ReturnBufferFlag) != 0);
    }

    public Symbol? FindFunction(uint rva, string? name = null)
    {
        var found = -1;

        for (var at = LowerBound(rva); at < ByAddress.Length && Rvas[ByAddress[at]] == rva; at++)
        {
            var index = ByAddress[at];

            if (!IsFunction(index))
            {
                continue;
            }

            if (found < 0)
            {
                found = index;
            }

            if (name is null || GetName(index) == name)
            {
                return new Symbol(this, index);
            }
        }

        return found < 0 ? null : new Symbol(this, found);
    }

    public Reader Read() => new(this);

    public SymbolIndex GetSearchIndex()
    {
        lock (IndexSync)
        {
            if (SearchIndex is { } reference && reference.TryGetTarget(out var cached))
            {
                return cached;
            }

            var index = SymbolIndex.Build(this);

            SearchIndex = new WeakReference<SymbolIndex>(index);

            return index;
        }
    }

    public List<Symbol> GetClassMembers(string className)
    {
        var members = new List<Symbol>();

        if (!ClassLookup.TryGetValue(className, out var id))
        {
            return members;
        }

        for (var index = 0; index < ClassIds.Length; index++)
        {
            if (ClassIds[index] == id && !IsGenerated(index))
            {
                members.Add(new Symbol(this, index));
            }
        }

        return members;
    }

    public static SymbolTable Load(DiaResolver resolver)
    {
        using var symbols = resolver.ReadSymbols(includeSignature: true, includeDecoratedName: true);

        var builder = new Builder(symbols.Count);

        while (symbols.MoveNext())
        {
            builder.Add(symbols.Rva, symbols.IsFunction, symbols.Name, symbols.Signature, symbols.DecoratedName);
        }

        return builder.Build();
    }

    private string GetText(int index, int field)
    {
        lock (Sync)
        {
            var chunk = Cache[(index >> ChunkShift) % Cache.Length] ??= new TextChunk();

            if (chunk.Index != index >> ChunkShift)
            {
                Decompress(index >> ChunkShift, chunk);
            }

            return Encoding.UTF8.GetString(chunk.GetText(index & ChunkMask, field));
        }
    }

    private void Decompress(int chunk, TextChunk target)
    {
        var fields = Math.Min(ChunkSize, Count - (chunk << ChunkShift)) * FieldCount;

        target.Index = -1;

        using var stream = new ZLibStream(new MemoryStream(Chunks[chunk]), CompressionMode.Decompress);

        stream.ReadExactly(MemoryMarshal.AsBytes(target.Starts.AsSpan(1, fields)));

        target.Starts[0] = 0;

        for (var at = 1; at <= fields; at++)
        {
            target.Starts[at] += target.Starts[at - 1];
        }

        stream.ReadExactly(target.Resize(target.Starts[fields]));

        target.Fields = fields;

        target.Index = chunk;
    }

    private int LowerBound(uint rva)
    {
        var low = 0;

        var high = ByAddress.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (Rvas[ByAddress[middle]] < rva)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    internal sealed class Builder(int capacity = 0)
    {
        private const int InitialTextLength = 1 << 16;

        private List<uint> Rvas { get; } = new(capacity);

        private List<ushort> Flags { get; } = new(capacity);

        private List<short> Separators { get; } = new(capacity);

        private List<short> ParameterCounts { get; } = new(capacity);

        private List<int> FirstParameters { get; } = new(capacity);

        private List<string?> ReturnTypes { get; } = new(capacity);

        private List<int> NameHashes { get; } = new(capacity);

        private List<int?> ParameterHashes { get; } = new(capacity);

        private StringInternPool Types { get; } = new();

        private List<string> ParameterTypes { get; } = [];

        private List<int> ClassIds { get; } = new(capacity);

        private Dictionary<string, int> ClassIdLookup { get; } = new(StringComparer.Ordinal);

        private List<string> Classes { get; } = [];

        private List<byte[]> Chunks { get; } = [];

        private int[] PendingLengths { get; } = new int[ChunkSize * FieldCount];

        private byte[] PendingText { get; set; } = new byte[InitialTextLength];

        private int PendingCount { get; set; }

        private int PendingLength { get; set; }

        private MemoryStream Compressed { get; } = new();

        public void Add(uint rva,
                        bool isFunction,
                        ReadOnlySpan<char> name,
                        ReadOnlySpan<char> signature,
                        ReadOnlySpan<char> decoratedName)
        {
            var first = ParameterTypes.Count;

            var type = FunctionType.Free;

            string? returnType = null;

            var hasReturnBuffer = false;

            var parsed = isFunction
                         && FunctionSignature.TryParse(signature,
                                                       decoratedName,
                                                       Types,
                                                       ParameterTypes,
                                                       out type,
                                                       out returnType,
                                                       out hasReturnBuffer);

            Rvas.Add(rva);

            var flags = GetFlags(isFunction, decoratedName, parsed ? type : null, hasReturnBuffer);

            Flags.Add((ushort)(flags | (name.Contains('`') ? GeneratedFlag : 0)));

            var separator = SymbolsHelper.FindClassMethodSeparator(name);

            Separators.Add((short)separator);

            ClassIds.Add(separator > 0 ? GetClassId(name[..separator]) : -1);

            ParameterCounts.Add((short)(parsed ? ParameterTypes.Count - first : -1));

            FirstParameters.Add(first);

            ReturnTypes.Add(returnType);

            NameHashes.Add(string.GetHashCode(name));

            ParameterHashes.Add(signature.IsEmpty ? null : FunctionSignature.HashParameters(signature));

            AddText(NameField, name);

            AddText(SignatureField, signature);

            AddText(DecoratedField, decoratedName);

            PendingCount++;

            if (PendingCount == ChunkSize)
            {
                Flush();
            }
        }

        public SymbolTable Build()
        {
            Flush();

            var byAddress = GetAddressOrder();

            var flags = Flags.ToArray();

            SetFolding(byAddress, flags);

            return new SymbolTable([.. Rvas],
                                   flags,
                                   [.. Separators],
                                   [.. ParameterCounts],
                                   [.. FirstParameters],
                                   [.. ReturnTypes],
                                   [.. ParameterTypes],
                                   [.. ClassIds],
                                   [.. Classes],
                                   [.. Chunks],
                                   byAddress);
        }

        private int GetClassId(ReadOnlySpan<char> className)
        {
            var lookup = ClassIdLookup.GetAlternateLookup<ReadOnlySpan<char>>();

            if (lookup.TryGetValue(className, out var id))
            {
                return id;
            }

            var text = className.ToString();

            id = Classes.Count;

            ClassIdLookup[text] = id;

            Classes.Add(text);

            return id;
        }

        private void AddText(int field, ReadOnlySpan<char> text)
        {
            var required = PendingLength + Encoding.UTF8.GetMaxByteCount(text.Length);

            if (PendingText.Length < required)
            {
                var grown = new byte[BitOperations.RoundUpToPowerOf2((uint)required)];

                PendingText.AsSpan(0, PendingLength).CopyTo(grown);

                PendingText = grown;
            }

            var length = Encoding.UTF8.GetBytes(text, PendingText.AsSpan(PendingLength));

            PendingLengths[PendingCount * FieldCount + field] = length;

            PendingLength += length;
        }

        private void Flush()
        {
            if (PendingCount == 0)
            {
                return;
            }

            Compressed.SetLength(0);

            using (var stream = new ZLibStream(Compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                stream.Write(MemoryMarshal.AsBytes(PendingLengths.AsSpan(0, PendingCount * FieldCount)));

                stream.Write(PendingText.AsSpan(0, PendingLength));
            }

            Chunks.Add(Compressed.ToArray());

            PendingCount = 0;

            PendingLength = 0;
        }

        private int[] GetAddressOrder()
        {
            var keys = new ulong[Rvas.Count];

            var order = new int[Rvas.Count];

            for (var index = 0; index < keys.Length; index++)
            {
                keys[index] = (ulong)Rvas[index] << 32 | (uint)index;

                order[index] = index;
            }

            Array.Sort(keys, order);

            return order;
        }

        private void SetFolding(int[] byAddress, ushort[] flags)
        {
            var first = -1;

            int? parameters = null;

            foreach (var index in byAddress)
            {
                if ((flags[index] & FunctionFlag) == 0)
                {
                    continue;
                }

                if (first < 0 || Rvas[first] != Rvas[index])
                {
                    first = index;

                    parameters = ParameterHashes[index];

                    flags[index] |= FirstFlag;

                    continue;
                }

                parameters ??= ParameterHashes[index];

                var isOtherName = NameHashes[index] != NameHashes[first];

                if (isOtherName)
                {
                    flags[first] |= FoldedNameFlag;
                }

                if (ParameterHashes[index] is { } hash && hash != parameters)
                {
                    flags[first] |= FoldedParametersFlag;
                }

                if (isOtherName && (ClassIds[index] < 0 || ClassIds[index] != ClassIds[first]))
                {
                    flags[first] |= FoldedClassFlag;
                }
            }
        }

        private static ushort GetFlags(bool isFunction, ReadOnlySpan<char> decoratedName, FunctionType? parsedType, bool hasReturnBuffer)
        {
            if (!isFunction)
            {
                return 0;
            }

            var flags = FunctionFlag;

            if (decoratedName.StartsWith(ConstructorPrefix, StringComparison.Ordinal))
            {
                flags |= ConstructorFlag;
            }

            if ((parsedType ?? FunctionSignature.GetFunctionType(decoratedName)) is { } type)
            {
                flags |= (ushort)(((int)type + 1) << TypeShift);
            }

            if (hasReturnBuffer)
            {
                flags |= ReturnBufferFlag;
            }

            return flags;
        }
    }

    internal sealed class Reader(SymbolTable table)
    {
        private TextChunk Chunk { get; } = new();

        private char[] Characters { get; set; } = [];

        private int[] CharacterStarts { get; } = new int[ChunkSize * FieldCount + 1];

        private FunctionSignature? ParsedSignature { get; set; }

        private bool IsParsed { get; set; }

        public int Index { get; private set; } = -1;

        public uint Rva => table.GetRva(Index);

        public bool IsFunction => table.IsFunction(Index);

        public bool IsConstructor => table.IsConstructor(Index);

        public bool IsFirst => table.IsFirst(Index);

        public bool HasFoldedName => table.HasFoldedName(Index);

        public bool HasFoldedParameters => table.HasFoldedParameters(Index);

        public bool HasFoldedClass => table.HasFoldedClass(Index);

        public bool IsGenerated => table.IsGenerated(Index);

        public FunctionType? FunctionType => table.GetFunctionType(Index);

        public int? ParameterCount => table.GetParameterCount(Index);

        public ReadOnlySpan<char> Name => GetCharacters(NameField);

        public string ClassName => table.GetClassName(Index);

        public ReadOnlySpan<char> Signature => GetCharacters(SignatureField);

        public FunctionSignature? Parsed
        {
            get
            {
                if (!IsParsed)
                {
                    ParsedSignature = table.GetParsedSignature(Index);

                    IsParsed = true;
                }

                return ParsedSignature;
            }
        }

        public bool MoveNext()
        {
            if (Index + 1 >= table.Count)
            {
                return false;
            }

            Index++;

            IsParsed = false;

            return true;
        }

        private void Decode()
        {
            if (Characters.Length < Chunk.Starts[Chunk.Fields])
            {
                Characters = new char[BitOperations.RoundUpToPowerOf2((uint)Chunk.Starts[Chunk.Fields])];
            }

            var at = 0;

            for (var field = 0; field < Chunk.Fields; field++)
            {
                CharacterStarts[field] = at;

                at += Encoding.UTF8.GetChars(Chunk.GetField(field), Characters.AsSpan(at));
            }

            CharacterStarts[Chunk.Fields] = at;
        }

        private ReadOnlySpan<char> GetCharacters(int field)
        {
            if (Chunk.Index != Index >> ChunkShift)
            {
                table.Decompress(Index >> ChunkShift, Chunk);

                Decode();
            }

            var at = (Index & ChunkMask) * FieldCount + field;

            return Characters.AsSpan(CharacterStarts[at], CharacterStarts[at + 1] - CharacterStarts[at]);
        }
    }

    private sealed class TextChunk
    {
        public int Index { get; set; } = -1;

        public int Fields { get; set; }

        public int[] Starts { get; } = new int[ChunkSize * FieldCount + 1];

        public byte[] Text { get; private set; } = [];

        public ReadOnlySpan<byte> GetText(int entry, int field) => GetField(entry * FieldCount + field);

        public ReadOnlySpan<byte> GetField(int field) => Text.AsSpan(Starts[field], Starts[field + 1] - Starts[field]);

        public Span<byte> Resize(int length)
        {
            if (Text.Length < length)
            {
                Text = new byte[BitOperations.RoundUpToPowerOf2((uint)length)];
            }

            return Text.AsSpan(0, length);
        }
    }
}

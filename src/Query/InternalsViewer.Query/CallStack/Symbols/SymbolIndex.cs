using System.Text;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.Symbols;

/// <summary>
/// The class names, names and signatures of every public symbol in a PDB, packed for search
/// </summary>
/// <remarks>
/// A module such as <c>sqlmin</c> declares hundreds of thousands of public symbols, so each field is kept as one
/// lower-cased ASCII buffer with an offset per symbol rather than a string each: a fraction of the memory, and a
/// search is a single vectorised scan of the buffer. Only the address is kept alongside, as the members pane
/// already describes a symbol from its address, with its proper casing and signature, when it is shown.
///
/// Plain text matches anywhere in a field. Text with <c>*</c> or <c>?</c> is a pattern in WinDbg's style, matched
/// against the whole field, so <c>XeSqlPkg::*</c> finds the members of a class and <c>*::GetRow</c> the functions
/// named that. A pattern is checked against the name as well as the signature, since a signature starts with the
/// return type and would otherwise need a leading star to match at all.
/// </remarks>
internal sealed class SymbolIndex
{
    private SymbolIndex(PackedStrings classes, PackedStrings names, PackedStrings signatures, uint[] rvas, bool[] functions)
    {
        Classes = classes;
        Names = names;
        Signatures = signatures;
        Rvas = rvas;
        Functions = functions;
    }

    public int Count => Rvas.Length;

    private PackedStrings Classes { get; }

    private PackedStrings Names { get; }

    private PackedStrings Signatures { get; }

    private uint[] Rvas { get; }

    private bool[] Functions { get; }

    public static SymbolIndex Build(IEnumerable<SymbolDetail> symbols)
    {
        var classes = new PackedStrings.Builder();

        var names = new PackedStrings.Builder();

        var signatures = new PackedStrings.Builder();

        var rvas = new List<uint>();

        var functions = new List<bool>();

        foreach (var symbol in symbols)
        {
            if (symbol.Name.Contains('`'))
            {
                continue;
            }

            var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol.Name);

            classes.Add(separator > 0 ? symbol.Name[..separator] : string.Empty);

            names.Add(symbol.Name);

            signatures.Add(symbol.Signature.Length > 0 ? symbol.Signature : symbol.Name);

            rvas.Add(symbol.Rva);

            functions.Add(symbol.IsFunction);
        }

        return new SymbolIndex(classes.Build(), names.Build(), signatures.Build(), rvas.ToArray(), functions.ToArray());
    }

    /// <summary>
    /// The symbols whose chosen fields match the text, ignoring case, in the order the PDB lists them
    /// </summary>
    /// <remarks>
    /// No field chosen means every field. The module is not a field here, as one index is one module: the caller
    /// decides whether the module's name qualifies every symbol in it.
    /// </remarks>
    public IReadOnlyList<SymbolHit> Search(string text, SymbolSearchFields fields, int limit)
    {
        var query = SearchQuery.Parse(text);

        if (query is null || limit <= 0)
        {
            return [];
        }

        if (fields == SymbolSearchFields.None)
        {
            fields = SymbolSearchFields.All;
        }

        var entries = new SortedSet<int>();

        if (fields.HasFlag(SymbolSearchFields.Class))
        {
            Classes.Search(query, limit, entries);
        }

        if (fields.HasFlag(SymbolSearchFields.Signature))
        {
            if (query.IsPattern)
            {
                Names.Search(query, limit, entries);
            }

            Signatures.Search(query, limit, entries);
        }

        return entries.Take(limit).Select(Hit).ToList();
    }

    /// <summary>
    /// Whether one symbol, described in full, satisfies the same search the index ran
    /// </summary>
    /// <remarks>
    /// Several symbols can share an address when the linker folds identical code, so describing a hit by its address
    /// brings back its neighbours too. This re-applies the search to each so only real matches are kept.
    /// </remarks>
    public static bool Matches(string text, SymbolSearchFields fields, string className, string name, string signature)
    {
        var query = SearchQuery.Parse(text);

        if (query is null)
        {
            return false;
        }

        if (fields == SymbolSearchFields.None)
        {
            fields = SymbolSearchFields.All;
        }

        if (fields.HasFlag(SymbolSearchFields.Class) && query.IsMatch(className))
        {
            return true;
        }

        return fields.HasFlag(SymbolSearchFields.Signature)
               && (query.IsMatch(signature.Length > 0 ? signature : name) || (query.IsPattern && query.IsMatch(name)));
    }

    /// <summary>
    /// Splits a <c>module!symbol</c> search into its parts, the module being null when there is no <c>!</c>
    /// </summary>
    public static (string? Module, string Symbol) SplitModule(string text)
    {
        var trimmed = text.Trim();

        var separator = trimmed.IndexOf('!');

        return separator < 0 ? (null, trimmed) : (trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());
    }

    /// <summary>
    /// Whether a module's name satisfies the module part of a search: contains it, or matches it as a pattern
    /// </summary>
    public static bool ModuleMatches(string text, string module) => SearchQuery.Parse(text)?.IsMatch(module) == true;

    /// <summary>
    /// The first symbols in the index, for a search satisfied by the module's name alone
    /// </summary>
    public IReadOnlyList<SymbolHit> First(int limit) => Enumerable.Range(0, Math.Min(limit, Count)).Select(Hit).ToList();

    private SymbolHit Hit(int entry) => new(Rvas[entry], Functions[entry]);

    /// <summary>
    /// A search text folded to lower-case ASCII, with the longest wildcard-free run picked out to scan for
    /// </summary>
    private sealed class SearchQuery
    {
        private SearchQuery(byte[] pattern, byte[] literal, bool isPattern)
        {
            Pattern = pattern;
            Literal = literal;
            IsPattern = isPattern;
        }

        public byte[] Pattern { get; }

        public byte[] Literal { get; }

        public bool IsPattern { get; }

        public static SearchQuery? Parse(string text)
        {
            var folded = PackedStrings.Fold(text.Trim());

            if (folded.Length == 0)
            {
                return null;
            }

            var isPattern = folded.Contains((byte)'*') || folded.Contains((byte)'?');

            return new SearchQuery(folded, isPattern ? LongestLiteral(folded) : folded, isPattern);
        }

        /// <summary>
        /// Whether the text satisfies the query: contains the literal, or matches the pattern as a whole
        /// </summary>
        public bool IsMatch(string value)
        {
            var folded = PackedStrings.Fold(value);

            return IsPattern ? Matches(folded) : folded.AsSpan().IndexOf(Literal) >= 0;
        }

        /// <summary>
        /// Whether the whole value matches the pattern, with <c>*</c> for any run and <c>?</c> for one character
        /// </summary>
        public bool Matches(ReadOnlySpan<byte> value)
        {
            var pattern = Pattern.AsSpan();

            var patternIndex = 0;

            var valueIndex = 0;

            var starPattern = -1;

            var starValue = -1;

            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
                {
                    patternIndex++;
                    valueIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starPattern = patternIndex++;
                    starValue = valueIndex;
                }
                else if (starPattern >= 0)
                {
                    patternIndex = starPattern + 1;
                    valueIndex = ++starValue;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private static byte[] LongestLiteral(byte[] pattern)
        {
            var best = ReadOnlySpan<byte>.Empty;

            var start = 0;

            for (var i = 0; i <= pattern.Length; i++)
            {
                if (i == pattern.Length || pattern[i] == '*' || pattern[i] == '?')
                {
                    if (i - start > best.Length)
                    {
                        best = pattern.AsSpan(start, i - start);
                    }

                    start = i + 1;
                }
            }

            return best.ToArray();
        }
    }

    /// <summary>
    /// Lower-cased ASCII strings in one buffer, each found by a binary search of its start offset
    /// </summary>
    private sealed class PackedStrings(byte[] bytes, int[] starts)
    {
        private byte[] Bytes { get; } = bytes;

        private int[] Starts { get; } = starts;

        private int Count => Starts.Length - 1;

        public static byte[] Fold(string text)
        {
            var folded = Encoding.ASCII.GetBytes(text);

            for (var i = 0; i < folded.Length; i++)
            {
                if (folded[i] is >= (byte)'A' and <= (byte)'Z')
                {
                    folded[i] += 'a' - 'A';
                }
            }

            return folded;
        }

        public void Search(SearchQuery query, int limit, SortedSet<int> entries)
        {
            if (query.Literal.Length == 0)
            {
                SearchEveryEntry(query, limit, entries);

                return;
            }

            var buffer = Bytes.AsSpan();

            var position = 0;

            var found = 0;

            while (found < limit)
            {
                var offset = buffer[position..].IndexOf(query.Literal);

                if (offset < 0)
                {
                    return;
                }

                var absolute = position + offset;

                var entry = EntryAt(absolute);

                var end = Starts[entry + 1];

                if (absolute + query.Literal.Length > end)
                {
                    position = absolute + 1;

                    continue;
                }

                if (!query.IsPattern || query.Matches(buffer[Starts[entry]..end]))
                {
                    entries.Add(entry);

                    found++;
                }

                position = end;
            }
        }

        private void SearchEveryEntry(SearchQuery query, int limit, SortedSet<int> entries)
        {
            var buffer = Bytes.AsSpan();

            var found = 0;

            for (var entry = 0; entry < Count && found < limit; entry++)
            {
                if (query.Matches(buffer[Starts[entry]..Starts[entry + 1]]))
                {
                    entries.Add(entry);

                    found++;
                }
            }
        }

        private int EntryAt(int offset)
        {
            var index = Array.BinarySearch(Starts, offset);

            return index >= 0 ? Math.Min(index, Count - 1) : ~index - 1;
        }

        public sealed class Builder
        {
            private readonly MemoryStream _bytes = new();

            private readonly List<int> _starts = [];

            public void Add(string text)
            {
                _starts.Add((int)_bytes.Length);

                _bytes.Write(Fold(text));
            }

            public PackedStrings Build()
            {
                _starts.Add((int)_bytes.Length);

                return new PackedStrings(_bytes.ToArray(), _starts.ToArray());
            }
        }
    }
}

/// <summary>
/// A symbol found by the index, by the address it is described from
/// </summary>
internal readonly record struct SymbolHit(uint Rva, bool IsFunction);

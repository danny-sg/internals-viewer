using System.Collections.Concurrent;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.CallStack;

public sealed class CallstackResolver(string symbolsPath) : IDisposable
{
    private const string StartOffset = "+0x0";

    private readonly record struct SymbolKey(string Guid, int Age, uint Rva);

    private static readonly ConcurrentDictionary<SymbolKey, string> TableNames = new();

    private static readonly ConcurrentDictionary<SymbolKey, string> DiaNames = new();

    private readonly ConcurrentDictionary<string, DiaResolver> _resolverCache = new();

    private readonly ConcurrentDictionary<SymbolKey, UnresolvedSymbolInfo> _unresolvedSymbols = [];

    private bool _disposed;

    public bool TryResolve(CallstackFrame frame, out string? symbol)
    {
        symbol = null;

        var key = new SymbolKey(frame.Module.Guid, frame.Module.Age, frame.Rva);

        if (!frame.Module.HasSymbols)
        {
            RecordUnresolved(key, UnresolvedReason.InvalidSymbolReference);

            return false;
        }

        symbol = ResolveInternal(frame, key);

        return true;
    }

    /// <summary>
    /// Lists the members of a class from the PDBs of every frame given, one group per module in the order given
    /// </summary>
    /// <remarks>
    /// Modules whose symbols declare nothing on the class are left out. Each module is searched on its own thread as
    /// the first search of a module indexes its whole PDB.
    /// </remarks>
    public async Task<ClassMemberListing> ListMembersAsync(IReadOnlyList<CallstackFrame> frames, string className)
    {
        var groups = await Task.WhenAll(frames.Select(f => Task.Run(() => ListMembers(f, className))));

        return new ClassMemberListing(className, groups.Where(g => g.Members.Count > 0).ToList());
    }

    /// <summary>
    /// Finds the public symbols whose name contains the text, across the PDBs of every frame given
    /// </summary>
    /// <remarks>
    /// Each module is searched on its own thread, as the first search of a module packs its whole PDB into an index.
    /// The limit applies per module, so a common word still shows something from each.
    /// </remarks>
    public async Task<IReadOnlyList<SymbolMatch>> SearchSymbolsAsync(IReadOnlyList<CallstackFrame> frames,
                                                                    string text,
                                                                    SymbolSearchFields fields,
                                                                    int limit)
    {
        var groups = await Task.WhenAll(frames.Select(f => Task.Run(() => SearchSymbols(f, text, fields, limit))));

        return groups.SelectMany(g => g).ToList();
    }

    /// <summary>
    /// Finds the public symbols in the frame's PDB whose chosen fields contain the text
    /// </summary>
    /// <remarks>
    /// Text with <c>|</c> in it is several searches, any of which qualifies a symbol. Within each, a <c>module!</c>
    /// prefix, as WinDbg writes a symbol, confines the search to modules matching it and searches the rest. Otherwise
    /// the module field is decided here: when it is searched and the module's name contains the text, the first
    /// symbols of the module qualify regardless of their own names.
    /// </remarks>
    public IReadOnlyList<SymbolMatch> SearchSymbols(CallstackFrame frame, string text, SymbolSearchFields fields, int limit)
    {
        if (SymbolTableCache.Get(symbolsPath, frame.Module) is not { } table)
        {
            return [];
        }

        var index = table.GetSearchIndex();

        return text.Split(SymbolSearchSuggestion.Alternative, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .SelectMany(alternative => SearchAlternative(frame, index, alternative, fields, limit))
                   .DistinctBy(m => (m.ClassName, m.Member.Signature))
                   .ToList();
    }

    private static List<SymbolMatch> SearchAlternative(CallstackFrame frame,
                                                       SymbolIndex index,
                                                       string text,
                                                       SymbolSearchFields fields,
                                                       int limit)
    {
        var (modulePart, symbolPart) = SymbolIndex.SplitModule(text);

        if (modulePart is not null && !SymbolIndex.ModuleMatches(modulePart, frame.Module.Name))
        {
            return [];
        }

        var moduleMatches = symbolPart.Length == 0
                            || (modulePart is null
                                && (fields == SymbolSearchFields.None || fields.HasFlag(SymbolSearchFields.Module))
                                && frame.Module.Name.Contains(symbolPart, StringComparison.OrdinalIgnoreCase));

        var hits = moduleMatches ? index.First(limit) : index.Search(symbolPart, fields, limit);

        var matches = new List<SymbolMatch>();

        foreach (var symbol in hits)
        {
            var className = symbol.ClassName;

            var prefix = className.Length > 0 ? $"{className}::" : string.Empty;

            matches.Add(new SymbolMatch(className,
                                        new ClassMember(frame.Module.Name,
                                                        symbol.MethodName,
                                                        RemoveScope(symbol.Signature, prefix),
                                                        symbol.Rva,
                                                        symbol.IsFunction)));
        }

        return matches;
    }

    /// <summary>
    /// Lists the members the frame's PDB declares on a class
    /// </summary>
    /// <remarks>
    /// Compiler-generated entries such as the vftable and the deleting destructors are left out.
    /// </remarks>
    public ClassMemberGroup ListMembers(CallstackFrame frame, string className)
    {
        if (SymbolTableCache.Get(symbolsPath, frame.Module) is not { } table)
        {
            return new ClassMemberGroup(frame.Module.Name, []);
        }

        var prefix = $"{className}::";

        var members = table.GetClassMembers(className)
                           .Select(s => new ClassMember(frame.Module.Name,
                                                        s.MethodName,
                                                        RemoveScope(s.Signature, prefix),
                                                        s.Rva,
                                                        s.IsFunction))
                           .DistinctBy(m => m.Signature, StringComparer.Ordinal)
                           .OrderBy(m => m.Signature, StringComparer.Ordinal)
                           .ToList();

        return new ClassMemberGroup(frame.Module.Name, members);
    }

    public Symbol? ResolveSymbol(CallstackFrame frame)
        => SymbolTableCache.Get(symbolsPath, frame.Module)?.FindFunction(FunctionRva(frame), frame.Function?.Name);

    public void ResolveSignatures(IEnumerable<CallstackFrame> frames)
    {
        var functions = frames.Where(f => f.Function is { Signature: null } && f.Module.HasSymbols)
                              .GroupBy(f => (f.Module.Pdb, f.Module.Guid, f.Module.Age, Rva: FunctionRva(f)));

        foreach (var function in functions)
        {
            if (ResolveSymbol(function.First()) is not { } symbol || symbol.Signature is not { Length: > 0 } signature)
            {
                continue;
            }

            var scoped = symbol.ClassName is { Length: > 0 } className ? RemoveScope(signature, $"{className}::") : signature;

            if (symbol.Parsed is { Type: FunctionType.Static })
            {
                scoped = $"static {scoped}";
            }

            foreach (var frame in function)
            {
                frame.Function!.Signature = scoped;
            }
        }
    }

    private static uint FunctionRva(CallstackFrame frame)
        => frame.Offset is { } offset && offset <= frame.Rva ? frame.Rva - offset : frame.Rva;

    private static string RemoveScope(string signature, string prefix)
    {
        var index = signature.IndexOf(prefix, StringComparison.Ordinal);

        var scoped = index < 0 ? signature : signature.Remove(index, prefix.Length);

        return scoped.Replace("(void)", "()", StringComparison.Ordinal);
    }

    private void RecordUnresolved(SymbolKey key, UnresolvedReason reason)
    {
        _unresolvedSymbols.AddOrUpdate(key,
                                       _ => new UnresolvedSymbolInfo { Attempts = 1, Reason = reason },
                                       (_, existing) =>
                                        {
                                            existing.Attempts++;

                                            return existing;
                                        });
    }

    private string ResolveInternal(CallstackFrame frame, SymbolKey key)
    {
        if (SymbolTableCache.Find(frame.Module)?.FindFunction(frame.Rva) is { } symbol)
        {
            return TableNames.GetOrAdd(key, static (_, found) => found.Name + StartOffset, symbol);
        }

        if (DiaNames.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var pdbPath = frame.Module.GetPdbPath(symbolsPath);

        if (!File.Exists(pdbPath))
        {
            RecordUnresolved(key, UnresolvedReason.SymbolFileMissing);

            return $"0x{frame.Rva:X}";
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        return DiaNames.GetOrAdd(key, resolver.Resolve(frame.Rva));
    }

    public void Dispose()
    {
        if(_disposed)
        {
            return;
        }

        foreach (var resolver in _resolverCache.Values)
        {
            resolver.Dispose();
        }

        _resolverCache.Clear();

        _disposed = true;
    }

    public enum UnresolvedReason
    {
        InvalidSymbolReference,
        SymbolFileMissing,
        SymbolLookupFailed
    }

    public sealed class UnresolvedSymbolInfo
    {
        public int Attempts { get; set; }

        public UnresolvedReason Reason { get; set; }
    }
}


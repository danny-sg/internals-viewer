using System.Collections.Concurrent;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.CallStack;

public sealed class CallstackResolver(string symbolsPath) : IDisposable
{
    private readonly record struct SymbolKey(string Guid, int Age, uint Rva);

    private readonly ConcurrentDictionary<SymbolKey, string> _symbolCache = new();

    private readonly ConcurrentDictionary<string, DiaResolver> _resolverCache = new();

    private readonly ConcurrentDictionary<string, Lazy<ClassMemberIndex>> _indexCache = new();

    private readonly ConcurrentDictionary<string, Lazy<SymbolIndex>> _searchIndexCache = new();

    private readonly ConcurrentDictionary<SymbolKey, UnresolvedSymbolInfo> _unresolvedSymbols = [];

    private bool _disposed;

    public bool TryResolve(CallstackFrame frame, out string? symbol)
    {
        symbol = null;

        var key = new SymbolKey(frame.Guid, frame.Age, frame.Rva);

        if (!HasSymbolInformation(frame))
        {
            RecordUnresolved(key, UnresolvedReason.InvalidSymbolReference);

            return false;
        }

        symbol = _symbolCache.GetOrAdd(key, _ => ResolveInternal(frame));

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
        var pdbPath = GetPdbPath(frame);

        if (!HasSymbolInformation(frame) || !File.Exists(pdbPath))
        {
            return [];
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        var index = _searchIndexCache.GetOrAdd(pdbPath, _ => new Lazy<SymbolIndex>(() => BuildSearchIndex(resolver),
                                                                                    LazyThreadSafetyMode.ExecutionAndPublication))
                                     .Value;

        return text.Split(SymbolSearchSuggestion.Alternative, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .SelectMany(alternative => SearchAlternative(frame, resolver, index, alternative, fields, limit))
                   .DistinctBy(m => (m.ClassName, m.Member.Signature))
                   .ToList();
    }

    private static List<SymbolMatch> SearchAlternative(CallstackFrame frame,
                                                       DiaResolver resolver,
                                                       SymbolIndex index,
                                                       string text,
                                                       SymbolSearchFields fields,
                                                       int limit)
    {
        var (modulePart, symbolPart) = SymbolIndex.SplitModule(text);

        if (modulePart is not null && !SymbolIndex.ModuleMatches(modulePart, frame.Module))
        {
            return [];
        }

        var moduleMatches = symbolPart.Length == 0
                            || (modulePart is null
                                && (fields == SymbolSearchFields.None || fields.HasFlag(SymbolSearchFields.Module))
                                && frame.Module.Contains(symbolPart, StringComparison.OrdinalIgnoreCase));

        var hits = moduleMatches ? index.First(limit) : index.Search(symbolPart, fields, limit);

        var matches = new List<SymbolMatch>();

        foreach (var hit in hits)
        {
            foreach (var detail in resolver.EnumerateSymbolsAtRva(hit.Rva))
            {
                if (detail.Name.Contains('`'))
                {
                    continue;
                }

                var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(detail.Name);

                var className = separator > 0 ? detail.Name[..separator] : string.Empty;

                if (!moduleMatches && !SymbolIndex.Matches(symbolPart, fields, className, detail.Name, detail.Signature))
                {
                    continue;
                }

                var name = separator > 0 ? detail.Name[(separator + 2)..] : detail.Name;

                var prefix = className.Length > 0 ? $"{className}::" : string.Empty;

                matches.Add(new SymbolMatch(className,
                                            new ClassMember(frame.Module,
                                                            name,
                                                            RemoveScope(detail.Signature, prefix),
                                                            detail.Rva,
                                                            detail.IsFunction)));
            }
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
        var pdbPath = GetPdbPath(frame);

        if (!HasSymbolInformation(frame) || !File.Exists(pdbPath))
        {
            return new ClassMemberGroup(frame.Module, []);
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        var index = _indexCache.GetOrAdd(pdbPath,
                                         _ => new Lazy<ClassMemberIndex>(() => ClassMemberIndex.Build(resolver),
                                                                         LazyThreadSafetyMode.ExecutionAndPublication))
                               .Value;

        var prefix = $"{className}::";

        var members = index.Find(className)
                           .Distinct()
                           .SelectMany(resolver.EnumerateSymbolsAtRva)
                           .Where(s => s.Name.StartsWith(prefix, StringComparison.Ordinal)
                                       && !s.Name.Contains('`')
                                       && ResolvedCallstackFrameParser.FindClassMethodSeparator(s.Name) == className.Length)
                           .Select(s => new ClassMember(frame.Module,
                                                        s.Name[prefix.Length..],
                                                        RemoveScope(s.Signature, prefix),
                                                        s.Rva,
                                                        s.IsFunction))
                           .DistinctBy(m => m.Signature, StringComparer.Ordinal)
                           .OrderBy(m => m.Signature, StringComparer.Ordinal)
                           .ToList();

        return new ClassMemberGroup(frame.Module, members);
    }

    /// <summary>
    /// The signature of the function containing the frame's address, or null when its symbols are not available
    /// </summary>
    public string? ResolveSignature(CallstackFrame frame)
    {
        var pdbPath = GetPdbPath(frame);

        if (!HasSymbolInformation(frame) || !File.Exists(pdbPath))
        {
            return null;
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        return resolver.EnumerateSymbolsAtRva(frame.Rva)
                       .Select(detail => detail.Signature)
                       .FirstOrDefault(signature => !string.IsNullOrEmpty(signature));
    }

    private static SymbolIndex BuildSearchIndex(DiaResolver resolver) =>
        SymbolIndex.Build(resolver.EnumerateSymbolDetails(string.Empty));

    private static string RemoveScope(string signature, string prefix)
    {
        var index = signature.IndexOf(prefix, StringComparison.Ordinal);

        var scoped = index < 0 ? signature : signature.Remove(index, prefix.Length);

        return scoped.Replace("(void)", "()", StringComparison.Ordinal);
    }

    private static bool HasSymbolInformation(CallstackFrame frame)
    {
        return !string.IsNullOrWhiteSpace(frame.Pdb)
               && !string.IsNullOrWhiteSpace(frame.Guid)
               && frame.Guid != "00000000-0000-0000-0000-000000000000"
               && frame.Age > 0;
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

    private string ResolveInternal(CallstackFrame frame)
    {
        var pdbPath = GetPdbPath(frame);

        if (!File.Exists(pdbPath))
        {
            RecordUnresolved(new SymbolKey(frame.Guid, frame.Age, frame.Rva), UnresolvedReason.SymbolFileMissing);

            return $"0x{frame.Rva:X}";
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        return resolver.Resolve(frame.Rva);
    }

    private string GetPdbPath(CallstackFrame frame)
    {
        var identifier = $"{frame.Guid.Replace("-", string.Empty)}{frame.Age}";

        return Path.Combine(symbolsPath, frame.Pdb, identifier.ToUpperInvariant(), frame.Pdb);
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


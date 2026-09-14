using System.Collections.Concurrent;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack;

public sealed class CallstackResolver(string symbolsPath) : IDisposable
{
    private readonly record struct SymbolKey(string Guid, int Age, uint Rva);

    private readonly ConcurrentDictionary<SymbolKey, string> _symbolCache = new();

    private readonly ConcurrentDictionary<string, DiaResolver> _resolverCache = new();

    private readonly ConcurrentDictionary<string, Lazy<ClassMemberIndex>> _indexCache = new();

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


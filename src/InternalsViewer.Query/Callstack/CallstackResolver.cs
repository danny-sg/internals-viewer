using InternalsViewer.Query.CallStack.Dia;
using System.Collections.Concurrent;

namespace InternalsViewer.Query.Callstack;

public sealed class CallstackResolver(string symbolsPath) : IDisposable
{
    private readonly record struct SymbolKey(string Guid, int Age, uint Rva);

    private readonly ConcurrentDictionary<SymbolKey, string> _symbolCache = new();

    private readonly ConcurrentDictionary<string, DiaResolver> _resolverCache = new();

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


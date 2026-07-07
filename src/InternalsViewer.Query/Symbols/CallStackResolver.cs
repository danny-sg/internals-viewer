using System.Collections.Concurrent;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Symbols;

public sealed class CallStackResolver(string symbolsPath)
{
    private readonly ConcurrentDictionary<SymbolKey, string> _symbolCache = new();

    private readonly ConcurrentDictionary<string, DiaResolver> _resolverCache = new();

    public string Resolve(CallStackFrame frame)
    {
        if (!HasSymbolInformation(frame))
        {
            return $"0x{frame.Rva:X}";
        }

        return _symbolCache.GetOrAdd(new SymbolKey(frame.Guid, frame.Age, frame.Rva), _ => ResolveInternal(frame));
    }

    private static bool HasSymbolInformation(CallStackFrame frame)
    {
        return
            !string.IsNullOrWhiteSpace(frame.Pdb)
            && !string.IsNullOrWhiteSpace(frame.Guid)
            && frame.Guid != "00000000-0000-0000-0000-000000000000"
            && frame.Age > 0;
    }


    private string ResolveInternal(CallStackFrame frame)
    {
        var pdbPath = GetPdbPath(frame);

        if (!File.Exists(pdbPath))
        {
            return $"0x{frame.Rva:X}";
        }

        var resolver = _resolverCache.GetOrAdd(pdbPath, path => new DiaResolver(path));

        return resolver.Resolve(frame.Rva);
    }

    private string GetPdbPath(CallStackFrame frame)
    {
        var identifier = $"{frame.Guid.Replace("-", string.Empty)}{frame.Age}";

        return Path.Combine(symbolsPath, frame.Pdb, identifier.ToUpperInvariant(), frame.Pdb);
    }
}
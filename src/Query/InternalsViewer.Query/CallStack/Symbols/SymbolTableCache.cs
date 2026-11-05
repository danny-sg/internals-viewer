using System.Collections.Concurrent;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.Symbols;

internal static class SymbolTableCache
{
    private static ConcurrentDictionary<(string Pdb, string Guid, int Age), Lazy<SymbolTable>> Tables { get; } = new();

    public static SymbolTable? Find(CallstackModule module)
        => Tables.TryGetValue(GetKey(module), out var table) && table.IsValueCreated ? table.Value : null;

    public static SymbolTable? Get(string symbolsPath, CallstackModule module)
    {
        if (!module.HasSymbols)
        {
            return null;
        }

        var path = module.GetPdbPath(symbolsPath);

        if (!File.Exists(path))
        {
            return Find(module);
        }

        var key = GetKey(module);

        var table = Tables.GetOrAdd(key, _ => new Lazy<SymbolTable>(() => Build(path), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return table.Value;
        }
        catch
        {
            Tables.TryRemove(KeyValuePair.Create(key, table));

            throw;
        }
    }

    private static (string Pdb, string Guid, int Age) GetKey(CallstackModule module) => (module.Pdb, module.Guid, module.Age);

    private static SymbolTable Build(string path)
    {
        using var resolver = new DiaResolver(path);

        return SymbolTable.Load(resolver);
    }
}

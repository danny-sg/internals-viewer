using System.Collections.Concurrent;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.CallStack;

public sealed record CallstackModule(string Name, string Pdb = "", string Guid = "", int Age = 0)
{
    private static readonly ConcurrentDictionary<(string Name, string Pdb, string Guid, int Age), CallstackModule> Shared = new();

    public static CallstackModule Empty { get; } = new(string.Empty);

    public ModuleCategory Category => CategoryMappings.Default.GetModuleCategory(Name);

    public bool HasSymbols => !string.IsNullOrWhiteSpace(Pdb)
                              && !string.IsNullOrWhiteSpace(Guid)
                              && Guid != "00000000-0000-0000-0000-000000000000"
                              && Age > 0;

    public string SymbolId => SymbolDownloader.SymbolId(Guid, Age);

    public static CallstackModule Get(string name, string pdb = "", string guid = "", int age = 0)
        => Shared.GetOrAdd((name, pdb, guid, age), static key => new CallstackModule(key.Name, key.Pdb, key.Guid, key.Age));

    public string GetPdbPath(string symbolsPath) => Path.Combine(symbolsPath, Pdb, SymbolId, Pdb);

    public override string ToString() => Name;
}

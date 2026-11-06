using System.Collections.Concurrent;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed record FrameModule(string Name, string Pdb = "", string Guid = "", int Age = 0)
{
    private static readonly ConcurrentDictionary<(string Name, string Pdb, string Guid, int Age), FrameModule> Shared = new();

    public static FrameModule Empty { get; } = new(string.Empty);

    public ModuleCategory Category => CategoryMappings.Default.GetModuleCategory(Name);

    public bool HasSymbols => !string.IsNullOrWhiteSpace(Pdb)
                              && !string.IsNullOrWhiteSpace(Guid)
                              && Guid != "00000000-0000-0000-0000-000000000000"
                              && Age > 0;

    public string SymbolId => SymbolDownloader.SymbolId(Guid, Age);

    public static FrameModule Get(string name, string pdb = "", string guid = "", int age = 0)
        => Shared.GetOrAdd((name, pdb, guid, age), static key => new FrameModule(key.Name, key.Pdb, key.Guid, key.Age));

    public string GetPdbPath(string symbolsPath) => Path.Combine(symbolsPath, Pdb, SymbolId, Pdb);

    public override string ToString() => Name;
}

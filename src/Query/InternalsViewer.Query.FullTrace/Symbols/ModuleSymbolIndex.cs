using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Symbols;

internal sealed class ModuleSymbolIndex
{
    private const uint ImageTail = 0x10000;

    private const uint FunctionReach = 0x4000;

    private const uint DataReach = 0x100;

    public ModuleSymbolIndex(IEnumerable<(string Module, ulong Start, SymbolTable Table)> modules)
    {
        Modules = [.. modules.OrderBy(m => m.Start)];

        Starts = [.. Modules.Select(m => m.Start)];
    }

    public static ModuleSymbolIndex Empty { get; } = new([]);

    private (string Module, ulong Start, SymbolTable Table)[] Modules { get; }

    private ulong[] Starts { get; }

    public ModuleAddress? Find(ulong value)
    {
        if (Locate(value) is not ({ } module, { } table, var rva, var symbol))
        {
            return null;
        }

        if (symbol < 0)
        {
            return new ModuleAddress(module, rva, null, 0);
        }

        var distance = rva - table.GetRva(symbol);

        return distance <= (table.IsFunction(symbol) ? FunctionReach : DataReach)
            ? new ModuleAddress(module, rva, table.GetName(symbol), distance)
            : new ModuleAddress(module, rva, null, 0);
    }

    public string? GetDecoratedName(ulong value)
        => Locate(value) is (_, { } table, var rva, >= 0 and var symbol) && table.GetRva(symbol) == rva
            ? table.GetDecoratedName(symbol)
            : null;

    public string? GetSignature(ulong value)
        => Locate(value) is (_, { } table, var rva, >= 0 and var symbol) && table.GetRva(symbol) == rva
            ? table.GetSignature(symbol)
            : null;

    private (string Module, SymbolTable Table, uint Rva, int Symbol)? Locate(ulong value)
    {
        var index = SortedSearch.FirstAfter(Starts, value) - 1;

        if (index < 0)
        {
            return null;
        }

        var (module, start, table) = Modules[index];

        var offset = value - start;

        if (offset > table.LastRva + ImageTail)
        {
            return null;
        }

        var rva = (uint)offset;

        return (module, table, rva, table.FindAtOrBelow(rva));
    }
}
